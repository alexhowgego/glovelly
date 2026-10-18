using System.Net;
using System.Text;
using System.Net.Http.Headers;
using Glovelly.Api.Data;
using Glovelly.Api.Models;
using Glovelly.Api.Services;
using Glovelly.Api.Tests.Infrastructure;
using Google;
using Google.Apis.Http;
using Google.Apis.Services;
using Google.Apis.Storage.v1;
using Google.Cloud.Storage.V1;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace Glovelly.Api.Tests;

public sealed class GcsBlobStoreTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SaveAsync_DefaultOptionsSendFullObjectChecksum(bool nonSeekable)
    {
        using var handler = new UploadHandler(rejectUpload: false);
        using var service = CreateService(handler);
        var store = CreateStore(service);
        await using Stream content = nonSeekable
            ? new NonSeekableStream("123456789"u8.ToArray())
            : new MemoryStream("123456789"u8.ToArray());

        await store.SaveAsync(new BlobWriteRequest("receipt", content, "application/pdf"), TestContext.Current.CancellationToken);

        Assert.Equal("crc32c=4waSgw==", handler.Checksum);
        Assert.Equal("123456789", handler.UploadedContent);
        Assert.Equal(1, handler.UploadRequests);
    }

    [Fact]
    public async Task SaveAsync_ServerChecksumRejectionPropagatesWithoutCleanupRequest()
    {
        using var handler = new UploadHandler(rejectUpload: true);
        using var service = CreateService(handler);
        var store = CreateStore(service);
        await using var content = new MemoryStream("123456789"u8.ToArray());

        var exception = await Assert.ThrowsAsync<GoogleApiException>(() => store.SaveAsync(
            new BlobWriteRequest("receipt", content, "application/pdf"), TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.BadRequest, exception.HttpStatusCode);
        Assert.Equal("crc32c=4waSgw==", handler.Checksum);
        Assert.Equal(1, handler.UploadRequests);
        Assert.Equal(0, handler.DeleteRequests);
    }

    [Fact]
    public async Task AttachmentUpload_ChecksumRejectionCreatesNoMetadataOrSuccessEvent()
    {
        using var handler = new UploadHandler(rejectUpload: true);
        using var service = CreateService(handler);
        var events = new RecordingEventPublisher();
        await using var factory = new GlovellyApiFactory();
        await using var configured = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IBlobStore>();
            services.AddSingleton<IBlobStore>(CreateStore(service));
            services.RemoveAll<IWorkspaceEventPublisher>();
            services.AddSingleton<IWorkspaceEventPublisher>(events);
        }));
        using var client = configured.CreateClient();
        var gigId = Guid.NewGuid();
        var expenseId = Guid.NewGuid();
        using (var scope = configured.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Gigs.Add(new Gig
            {
                Id = gigId, ClientId = TestData.RiversideId, Title = "Checksum failure", Venue = "Venue",
                Date = new DateOnly(2026, 1, 1), Status = GigStatus.Confirmed, CreatedByUserId = TestAuthContext.UserId,
                Expenses = [new GigExpense { Id = expenseId, Description = "Taxi", Amount = 10m }],
            });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
        using var form = new MultipartFormDataContent();
        using var file = new ByteArrayContent("123456789"u8.ToArray());
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(file, "file", "receipt.pdf");

        var exception = await Assert.ThrowsAsync<GoogleApiException>(() => client.PostAsync(
            $"/gigs/{gigId}/expenses/{expenseId}/attachments", form, TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.BadRequest, exception.HttpStatusCode);
        Assert.Equal("crc32c=4waSgw==", handler.Checksum);
        using var verification = configured.Services.CreateScope();
        var context = verification.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Empty(context.ExpenseAttachments);
        Assert.Single(context.GigExpenses);
        Assert.Empty(events.Events);
    }

    private sealed class RecordingEventPublisher : IWorkspaceEventPublisher
    {
        public List<WorkspaceEvent> Events { get; } = [];
        public Task PublishAsync(Guid? userId, WorkspaceEvent workspaceEvent, CancellationToken cancellationToken = default)
        {
            Events.Add(workspaceEvent);
            return Task.CompletedTask;
        }
    }

    private static StorageService CreateService(UploadHandler handler) => new(new BaseClientService.Initializer
    {
        HttpClientFactory = new UploadHttpClientFactory(handler),
        GZipEnabled = false,
    });

    private static GcsBlobStore CreateStore(StorageService service) => new(
        new StorageClientImpl(service), Options.Create(new BlobStorageSettings { BucketName = "test-bucket" }));

    private sealed class UploadHttpClientFactory(HttpMessageHandler handler) : Google.Apis.Http.IHttpClientFactory
    {
        public ConfigurableHttpClient CreateHttpClient(CreateHttpClientArgs args) =>
            new(new ConfigurableMessageHandler(handler));
    }

    private sealed class UploadHandler(bool rejectUpload) : HttpMessageHandler
    {
        public string? Checksum { get; private set; }
        public string? UploadedContent { get; private set; }
        public int UploadRequests { get; private set; }
        public int DeleteRequests { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Method == HttpMethod.Post)
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK);
                response.Headers.Location = new Uri("https://storage.googleapis.com/upload/storage/v1/b/test-bucket/o?uploadType=resumable&upload_id=test-session");
                return response;
            }

            if (request.Method == HttpMethod.Put)
            {
                UploadRequests++;
                Checksum = request.Headers.TryGetValues("x-goog-hash", out var values) ? Assert.Single(values) : null;
                UploadedContent = await request.Content!.ReadAsStringAsync(cancellationToken);
                return new HttpResponseMessage(rejectUpload ? HttpStatusCode.BadRequest : HttpStatusCode.OK)
                {
                    Content = new StringContent(rejectUpload
                        ? """{"error":{"code":400,"message":"CRC32C checksum mismatch","errors":[{"reason":"invalid","message":"CRC32C checksum mismatch"}]}}"""
                        : """{"bucket":"test-bucket","name":"receipt","crc32c":"4waSgw=="}""", Encoding.UTF8, "application/json"),
                };
            }

            if (request.Method == HttpMethod.Delete) DeleteRequests++;
            throw new InvalidOperationException($"Unexpected storage request: {request.Method}");
        }
    }

    private sealed class NonSeekableStream(byte[] bytes) : MemoryStream(bytes)
    {
        public override bool CanSeek => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }
        public override long Seek(long offset, SeekOrigin loc) => throw new NotSupportedException();
    }
}
