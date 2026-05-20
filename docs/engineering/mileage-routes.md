# Mileage And Routes

Glovelly can estimate driving mileage for a saved gig using Google Routes API.

The feature is intentionally a suggestion workflow. Google calculates a route estimate, the frontend fills the existing `TravelMiles` input, and the user can edit the value before saving the gig. Manual mileage remains the source of truth for invoice generation.

## Runtime Flow

1. The user opens a saved gig and clicks `Estimate mileage`.
2. The frontend calls `POST /gigs/{id}/mileage-estimate` with the current gig location as `destination` and `roundTrip: true`.
3. The backend authorises access through the normal gig visibility rules.
4. The backend uses the seller profile postcode and country as the origin when the request does not provide one.
5. `IMileageEstimationService` calculates the estimate.
6. The frontend writes the returned `distanceMiles` into the existing travel miles field.

The estimate endpoint does not save the gig. Saving still happens through the normal gig create/update flow.

## Provider

Google Routes is implemented by `GoogleRoutesMileageEstimationService`.

The service calls:

```text
https://routes.googleapis.com/distanceMatrix/v2:computeRouteMatrix
```

It sends:

- `X-Goog-Api-Key`
- `X-Goog-FieldMask: originIndex,destinationIndex,duration,distanceMeters,status,condition`
- one address origin
- one destination, either an address or a Google place ID
- `travelMode: DRIVE`
- `routingPreference: TRAFFIC_UNAWARE`

Round-trip estimates are calculated by doubling the returned one-way distance and duration.

## Configuration

Local non-secret defaults live in `appsettings.Development.json`:

```json
{
  "Mileage": {
    "GoogleRoutes": {
      "Endpoint": "https://routes.googleapis.com/distanceMatrix/v2:computeRouteMatrix",
      "TravelMode": "DRIVE",
      "RoutingPreference": "TRAFFIC_UNAWARE"
    }
  }
}
```

The API key is a secret. For local development, store it with user secrets:

```bash
cd backend/Glovelly.Api
dotnet user-secrets set "Mileage:GoogleRoutes:ApiKey" "your-google-routes-api-key"
```

For deployed environments, provide `Mileage__GoogleRoutes__ApiKey` through Google Secret Manager or equivalent secret-backed runtime configuration.

If no API key is configured, Glovelly uses `DisabledMileageEstimationService` and returns a provider failure. The user can still enter mileage manually.

## Error Handling

The backend returns validation errors when a gig, destination, or origin cannot be resolved.

Provider failures return a non-saving failure response. Common cases include:

- Google Routes API key missing or invalid
- quota or billing failure
- unroutable origin/destination
- malformed provider response

The frontend surfaces the message in the gig status area and leaves the travel miles input editable.

## Follow-Up

Dedicated travel-origin settings should replace the temporary seller-profile origin fallback. Seller profile address is invoice-facing and may not be the user's preferred travel start point.
