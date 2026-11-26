namespace Glovelly.Api.Services;

public sealed class FeatureNotEntitledException()
    : Exception("Paid-income reporting requires Solo or Pro. Your underlying records remain available.");
