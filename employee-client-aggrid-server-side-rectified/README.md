# Employee Client — AG Grid Server-Side Demo

Clean portfolio extraction of the AG Grid Community server-side implementation
from the Employee Client MAUI/Blazor Hybrid project.

## Included
- `API/ComplaintsController.cs` — focused paginated/filter/sort endpoint only
- `API/FilterCondition.cs` — filtering helper/model extracted with the endpoint
- `API/PaginatedResult.cs` — server response wrapper
- `Blazor/CBS_tra_NewComplaint.razor` — AG Grid Blazor page
- `wwwroot/jsshared/aggridh.js` — AG Grid JavaScript interop
- `wwwroot/aggrid/` — AG Grid Community library and CSS

## Endpoint

`GET /api/complaints/paginated/{compCode}`

Supports page number/size, dynamic filters including AND/OR, text search,
multi-column sorting, total record count, and DTO projection.

## Integration

The endpoint still expects the application's existing EF Core DbContext,
`CbsTraNewComplaint` entity, `ClientProject` relationship, `ComplaintDto`,
and related namespaces. Authentication and unrelated Employee Client API
methods are intentionally excluded.

No credentials, connection strings, build output, or Visual Studio metadata
are included.
