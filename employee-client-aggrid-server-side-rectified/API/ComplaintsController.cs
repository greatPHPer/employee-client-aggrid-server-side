// Focused AG Grid server-side endpoint extracted from the original API.
// Unrelated authentication/controller actions are intentionally excluded.
//
// Integrate this action into your own controller and retain your application's
// actual DbContext/entity namespaces and using directives.

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using System.Text.Json;

[HttpGet("complaints/paginated/{compCode}")]
        //    public async Task<IActionResult> GetPaginatedComplaints(
        //string compCode,
        //[FromQuery] int pageNumber = 1,
        //[FromQuery] int pageSize = 10,
        //[FromQuery] string? searchCols = "",
        //[FromQuery] string? sortCols = "Complaint_id:DESC",
        //[FromQuery] string? searchTerm = "",
        //[FromQuery] string? sortDirection = "")
        //    {
        //        try
        //        {
        //            // 1. Start with base query using Include to join Cbs_mas_clientproject
        //            var query = _context.CbsTraNewComplaints
        //                .Include(c => c.ClientProject)
        //                .Where(c => c.Comp_code == compCode);

        //            // 2. Handle Dynamic Grid Filters (AgGrid/FluentUI Advanced Filtering JSON)
        //            if (!string.IsNullOrWhiteSpace(searchCols) && searchCols.Trim().StartsWith("["))
        //            {
        //                try
        //                {
        //                    var expressions = System.Text.Json.JsonSerializer.Deserialize<List<FilterCondition>>(
        //                        searchCols,
        //                        new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true }
        //                    );

        //                    if (expressions != null && expressions.Any())
        //                    {
        //                        foreach (var expr in expressions)
        //                        {
        //                            if (string.IsNullOrWhiteSpace(expr.SearchValue)) continue;
        //                            string val = expr.SearchValue.Trim().ToLower();

        //                            // Build filtering expression matching columns dynamically
        //                            switch (expr.SelectedColumn.ToLower())
        //                            {
        //                                case "complaint_id":
        //                                    if (int.TryParse(val, out int idVal))
        //                                        query = query.Where(c => c.Complaint_id == idVal);
        //                                    break;
        //                                case "client_code":
        //                                    query = query.Where(c => c.Client_code.ToLower().Contains(val));
        //                                    break;
        //                                case "project_code":
        //                                    query = query.Where(c => c.Project_code.ToLower().Contains(val));
        //                                    break;
        //                                case "project_name":
        //                                    query = query.Where(c => c.ClientProject != null && c.ClientProject.Project_name!.ToLower().Contains(val));
        //                                    break;
        //                                case "issue_description":
        //                                    query = query.Where(c => c.Issue_description.ToLower().Contains(val));
        //                                    break;
        //                                case "issue_raised_by":
        //                                    query = query.Where(c => c.Issue_raised_by != null && c.Issue_raised_by.ToLower().Contains(val));
        //                                    break;
        //                            }
        //                        }
        //                    }
        //                }
        //                catch (System.Text.Json.JsonException jsonEx)
        //                {
        //                    _logger.LogWarning(jsonEx, "Failed parsing search filters array JSON payload.");
        //                }
        //            }
        //            // 3. Handle Simple Text Search Term Fallback
        //            else if (!string.IsNullOrWhiteSpace(searchTerm))
        //            {
        //                string term = searchTerm.Trim().ToLower();
        //                query = query.Where(c =>
        //                    c.Client_code.ToLower().Contains(term) ||
        //                    c.Project_code.ToLower().Contains(term) ||
        //                    c.Issue_description.ToLower().Contains(term) ||
        //                    (c.ClientProject != null && c.ClientProject.Project_name!.ToLower().Contains(term)) ||
        //                    (c.Issue_raised_by != null && c.Issue_raised_by.ToLower().Contains(term))
        //                );
        //            }

        //            // 4. Calculate total records safely from the filter configuration
        //            int totalRecords = await query.CountAsync();

        //            // 5. Apply Dynamic Sorting
        //            if (!string.IsNullOrWhiteSpace(sortCols))
        //            {
        //                var parts = sortCols.Split(':');
        //                string sortField = parts[0].Trim().ToLower();
        //                bool isDesc = parts.Length > 1 && parts[1].Trim().Equals("DESC", StringComparison.OrdinalIgnoreCase);

        //                switch (sortField)
        //                {
        //                    case "complaint_id": query = isDesc ? query.OrderByDescending(c => c.Complaint_id) : query.OrderBy(c => c.Complaint_id); break;
        //                    case "client_code": query = isDesc ? query.OrderByDescending(c => c.Client_code) : query.OrderBy(c => c.Client_code); break;
        //                    case "project_code": query = isDesc ? query.OrderByDescending(c => c.Project_code) : query.OrderBy(c => c.Project_code); break;
        //                    case "project_name": query = isDesc ? query.OrderByDescending(c => c.ClientProject!.Project_name) : query.OrderBy(c => c.ClientProject!.Project_name); break;
        //                    case "issue_description": query = isDesc ? query.OrderByDescending(c => c.Issue_description) : query.OrderBy(c => c.Issue_description); break;
        //                    case "inserted_dt": query = isDesc ? query.OrderByDescending(c => c.Inserted_dt) : query.OrderBy(c => c.Inserted_dt); break;
        //                    case "issue_raised_by": query = isDesc ? query.OrderByDescending(c => c.Issue_raised_by) : query.OrderBy(c => c.Issue_raised_by); break;
        //                    default: query = query.OrderByDescending(c => c.Complaint_id); break;
        //                }
        //            }
        //            else
        //            {
        //                query = query.OrderByDescending(c => c.Complaint_id);
        //            }

        //            // 6. Pagination & Data Projection (Select)
        //            int skip = (pageNumber - 1) * pageSize;
        //            var paginatedList = await query
        //                .Skip(skip)
        //                .Take(pageSize)
        //                .Select(c => new ComplaintDto
        //                {
        //                    Complaint_id = c.Complaint_id,
        //                    Comp_code = c.Comp_code,
        //                    Client_code = c.Client_code,
        //                    Project_code = c.Project_code,
        //                    Project_name = c.ClientProject != null ? c.ClientProject.Project_name : "",
        //                    Issue_description = c.Issue_description,
        //                    Project_start_date = c.Project_start_date,
        //                    Project_completion_date = c.Project_completion_date,
        //                    Amc_start_date = c.Amc_start_date,
        //                    Amc_finish_date = c.Amc_finish_date,
        //                    AMC_Start_dt = c.Amc_start_date,
        //                    AMC_End_dt = c.Amc_finish_date,
        //                    Support_doc1 = c.Support_doc1,
        //                    Support_doc2 = c.Support_doc2,
        //                    Support_doc3 = c.Support_doc3,
        //                    Support_doc4 = c.Support_doc4,
        //                    Support_doc5 = c.Support_doc5,
        //                    Issue_raised_by = c.Issue_raised_by,
        //                    Issue_booked_date = c.Issue_booked_date,
        //                    Issue_allotted_to = c.Issue_allotted_to,
        //                    Issue_allotted_date = c.Issue_allotted_date,
        //                    Issue_closed_date = c.Issue_closed_date,
        //                    Inserted_dt = c.Inserted_dt,
        //                    Start_dt = c.ClientProject != null ? c.ClientProject.Start_dt : null,
        //                    Completed_dt = c.ClientProject != null ? c.ClientProject.Completed_dt : null
        //                })
        //                .ToListAsync();

        //            return Ok(new PaginatedResult<ComplaintDto> { Data = paginatedList, TotalRecords = totalRecords });
        //        }
        //        catch (Exception ex)
        //        {
        //            _logger.LogError(ex, "Error processing paginated complaints list.");
        //            return StatusCode(500, $"Internal server error: {ex.Message}");
        //        }
        //    }
        //[HttpGet("complaints/paginated/{compCode}")]
        //public async Task<IActionResult> GetPaginatedComplaints(
        //    string compCode,
        //    [FromQuery] int pageNumber = 1,
        //    [FromQuery] int pageSize = 10,
        //    [FromQuery] string? searchCols = "",
        //    [FromQuery] string? sortCols = "", // Now expects format like "client_code asc, project_code desc"
        //    [FromQuery] string? searchTerm = "",
        //    [FromQuery] string? sortDirection = "")
        //{
        //    try
        //    {
        //        // 1. Start with base query using Include to join Cbs_mas_clientproject
        //        var query = _context.CbsTraNewComplaints
        //            .Include(c => c.ClientProject)
        //            .Where(c => c.Comp_code == compCode);

        //        // 2. Handle Dynamic Grid Filters (Supports Multiple Filters via AND logic)
        //        if (!string.IsNullOrWhiteSpace(searchCols) && searchCols.Trim().StartsWith("["))
        //        {
        //            try
        //            {
        //                var expressions = System.Text.Json.JsonSerializer.Deserialize<List<FilterCondition>>(
        //                    searchCols,
        //                    new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true }
        //                );

        //                if (expressions != null && expressions.Any())
        //                {
        //                    foreach (var expr in expressions)
        //                    {
        //                        if (string.IsNullOrWhiteSpace(expr.SearchValue)) continue;
        //                        string val = expr.SearchValue.Trim().ToLower();

        //                        // Build filtering expression matching columns dynamically
        //                        switch (expr.SelectedColumn.ToLower())
        //                        {
        //                            case "complaint_id":
        //                                if (int.TryParse(val, out int idVal))
        //                                    query = query.Where(c => c.Complaint_id == idVal);
        //                                break;
        //                            case "client_code":
        //                                query = query.Where(c => c.Client_code.ToLower().Contains(val));
        //                                break;
        //                            case "project_code":
        //                                query = query.Where(c => c.Project_code.ToLower().Contains(val));
        //                                break;
        //                            case "project_name":
        //                                query = query.Where(c => c.ClientProject != null && c.ClientProject.Project_name!.ToLower().Contains(val));
        //                                break;
        //                            case "issue_description":
        //                                query = query.Where(c => c.Issue_description.ToLower().Contains(val));
        //                                break;
        //                            case "issue_raised_by":
        //                                query = query.Where(c => c.Issue_raised_by != null && c.Issue_raised_by.ToLower().Contains(val));
        //                                break;
        //                        }
        //                    }
        //                }
        //            }
        //            catch (System.Text.Json.JsonException jsonEx)
        //            {
        //                _logger.LogWarning(jsonEx, "Failed parsing search filters array JSON payload.");
        //            }
        //        }

        //        // 3. Handle Simple Text Search Term Fallback
        //        else if (!string.IsNullOrWhiteSpace(searchTerm))
        //        {
        //            string term = searchTerm.Trim().ToLower();
        //            query = query.Where(c =>
        //                c.Client_code.ToLower().Contains(term) ||
        //                c.Project_code.ToLower().Contains(term) ||
        //                c.Issue_description.ToLower().Contains(term) ||
        //                (c.ClientProject != null && c.ClientProject.Project_name!.ToLower().Contains(term)) ||
        //                (c.Issue_raised_by != null && c.Issue_raised_by.ToLower().Contains(term))
        //            );
        //        }

        //        // 4. Calculate total records safely from the filter configuration
        //        int totalRecords = await query.CountAsync();

        //        // 5. Apply Dynamic MULTIPLE Sorting
        //        if (!string.IsNullOrWhiteSpace(sortCols))
        //        {
        //            // Split by comma to get each sort rule (e.g., ["client_code asc", "project_code desc"])
        //            var sortParams = sortCols.Split(',', StringSplitOptions.RemoveEmptyEntries);
        //            IOrderedQueryable<CbsTraNewComplaint>? orderedQuery = null;

        //            foreach (var sortParam in sortParams)
        //            {
        //                // Split the column and the direction
        //                var parts = sortParam.Trim().Split(':', StringSplitOptions.RemoveEmptyEntries);
        //                if (parts.Length == 0) continue;

        //                string sortField = parts[0].ToLower();
        //                bool isDesc = parts.Length > 1 && parts[1].Equals("desc", StringComparison.OrdinalIgnoreCase);

        //                if (orderedQuery == null)
        //                {
        //                    // The FIRST sort column must use OrderBy / OrderByDescending
        //                    switch (sortField)
        //                    {
        //                        case "complaint_id": orderedQuery = isDesc ? query.OrderByDescending(c => c.Complaint_id) : query.OrderBy(c => c.Complaint_id); break;
        //                        case "client_code": orderedQuery = isDesc ? query.OrderByDescending(c => c.Client_code) : query.OrderBy(c => c.Client_code); break;
        //                        case "project_code": orderedQuery = isDesc ? query.OrderByDescending(c => c.Project_code) : query.OrderBy(c => c.Project_code); break;
        //                        case "project_name": orderedQuery = isDesc ? query.OrderByDescending(c => c.ClientProject!.Project_name) : query.OrderBy(c => c.ClientProject!.Project_name); break;
        //                        case "issue_description": orderedQuery = isDesc ? query.OrderByDescending(c => c.Issue_description) : query.OrderBy(c => c.Issue_description); break;
        //                        case "inserted_dt": orderedQuery = isDesc ? query.OrderByDescending(c => c.Inserted_dt) : query.OrderBy(c => c.Inserted_dt); break;
        //                        case "issue_raised_by": orderedQuery = isDesc ? query.OrderByDescending(c => c.Issue_raised_by) : query.OrderBy(c => c.Issue_raised_by); break;
        //                        default: orderedQuery = query.OrderByDescending(c => c.Complaint_id); break;
        //                    }
        //                }
        //                else
        //                {
        //                    // Any SUBSEQUENT sort columns must use ThenBy / ThenByDescending
        //                    switch (sortField)
        //                    {
        //                        case "complaint_id": orderedQuery = isDesc ? orderedQuery.ThenByDescending(c => c.Complaint_id) : orderedQuery.ThenBy(c => c.Complaint_id); break;
        //                        case "client_code": orderedQuery = isDesc ? orderedQuery.ThenByDescending(c => c.Client_code) : orderedQuery.ThenBy(c => c.Client_code); break;
        //                        case "project_code": orderedQuery = isDesc ? orderedQuery.ThenByDescending(c => c.Project_code) : orderedQuery.ThenBy(c => c.Project_code); break;
        //                        case "project_name": orderedQuery = isDesc ? orderedQuery.ThenByDescending(c => c.ClientProject!.Project_name) : orderedQuery.ThenBy(c => c.ClientProject!.Project_name); break;
        //                        case "issue_description": orderedQuery = isDesc ? orderedQuery.ThenByDescending(c => c.Issue_description) : orderedQuery.ThenBy(c => c.Issue_description); break;
        //                        case "inserted_dt": orderedQuery = isDesc ? orderedQuery.ThenByDescending(c => c.Inserted_dt) : orderedQuery.ThenBy(c => c.Inserted_dt); break;
        //                        case "issue_raised_by": orderedQuery = isDesc ? orderedQuery.ThenByDescending(c => c.Issue_raised_by) : orderedQuery.ThenBy(c => c.Issue_raised_by); break;
        //                    }
        //                }
        //            }

        //            // Override the base query with the newly ordered query
        //            query = orderedQuery ?? query.OrderByDescending(c => c.Complaint_id);
        //        }
        //        else
        //        {
        //            // Default fallback sorting
        //            query = query.OrderByDescending(c => c.Complaint_id);
        //        }

        //        // 6. Pagination & Data Projection (Select)
        //        int skip = (pageNumber - 1) * pageSize;
        //        var paginatedList = await query
        //            .Skip(skip)
        //            .Take(pageSize)
        //            .Select(c => new ComplaintDto
        //            {
        //                Complaint_id = c.Complaint_id,
        //                Comp_code = c.Comp_code,
        //                Client_code = c.Client_code,
        //                Project_code = c.Project_code,
        //                Project_name = c.ClientProject != null ? c.ClientProject.Project_name : "",
        //                Issue_description = c.Issue_description,
        //                Project_start_date = c.Project_start_date,
        //                Project_completion_date = c.Project_completion_date,
        //                Amc_start_date = c.Amc_start_date,
        //                Amc_finish_date = c.Amc_finish_date,
        //                AMC_Start_dt = c.Amc_start_date,
        //                AMC_End_dt = c.Amc_finish_date,
        //                Support_doc1 = c.Support_doc1,
        //                Support_doc2 = c.Support_doc2,
        //                Support_doc3 = c.Support_doc3,
        //                Support_doc4 = c.Support_doc4,
        //                Support_doc5 = c.Support_doc5,
        //                Issue_raised_by = c.Issue_raised_by,
        //                Issue_booked_date = c.Issue_booked_date,
        //                Issue_allotted_to = c.Issue_allotted_to,
        //                Issue_allotted_date = c.Issue_allotted_date,
        //                Issue_closed_date = c.Issue_closed_date,
        //                Inserted_dt = c.Inserted_dt,
        //                Start_dt = c.ClientProject != null ? c.ClientProject.Start_dt : null,
        //                Completed_dt = c.ClientProject != null ? c.ClientProject.Completed_dt : null
        //            })
        //            .ToListAsync();

        //        return Ok(new PaginatedResult<ComplaintDto> { Data = paginatedList, TotalRecords = totalRecords });
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Error processing paginated complaints list.");
        //        return StatusCode(500, $"Internal server error: {ex.Message}");
        //    }
        //}
        //private Expression BuildCondition(ParameterExpression param, FilterCondition expr)
        //{
        //    if (string.IsNullOrWhiteSpace(expr.SearchValue)) return null;
        //    string val = expr.SearchValue.Trim().ToLower();

        //    Expression property = null;
        //    Expression nullCheck = null;

        //    // 1. Resolve the Property based on the column name
        //    switch (expr.SelectedColumn.ToLower())
        //    {
        //        case "complaint_id":
        //            if (int.TryParse(val, out int idVal))
        //            {
        //                property = Expression.Property(param, "Complaint_id");
        //                return expr.FilterOperator == "Not Equals"
        //                    ? Expression.NotEqual(property, Expression.Constant(idVal))
        //                    : Expression.Equal(property, Expression.Constant(idVal));
        //            }
        //            return null;
        //        case "client_code":
        //            property = Expression.Property(param, "Client_code");
        //            nullCheck = Expression.NotEqual(property, Expression.Constant(null, typeof(string)));
        //            break;
        //        case "project_code":
        //            property = Expression.Property(param, "Project_code");
        //            nullCheck = Expression.NotEqual(property, Expression.Constant(null, typeof(string)));
        //            break;
        //        case "issue_description":
        //            property = Expression.Property(param, "Issue_description");
        //            nullCheck = Expression.NotEqual(property, Expression.Constant(null, typeof(string)));
        //            break;
        //        case "issue_raised_by":
        //            property = Expression.Property(param, "Issue_raised_by");
        //            nullCheck = Expression.NotEqual(property, Expression.Constant(null, typeof(string)));
        //            break;
        //        case "project_name":
        //            // Handle Navigation Property Safely
        //            var clientProject = Expression.Property(param, "ClientProject");
        //            property = Expression.Property(clientProject, "Project_name");

        //            var navCheck = Expression.NotEqual(clientProject, Expression.Constant(null));
        //            var propCheck = Expression.NotEqual(property, Expression.Constant(null, typeof(string)));
        //            nullCheck = Expression.AndAlso(navCheck, propCheck);
        //            break;
        //        default:
        //            return null;
        //    }

        //    // 2. Prepare String Methods
        //    var toLowerMethod = typeof(string).GetMethod("ToLower", Type.EmptyTypes);
        //    var containsMethod = typeof(string).GetMethod("Contains", new[] { typeof(string) });
        //    var startsWithMethod = typeof(string).GetMethod("StartsWith", new[] { typeof(string) });
        //    var endsWithMethod = typeof(string).GetMethod("EndsWith", new[] { typeof(string) });

        //    var searchConstant = Expression.Constant(val);
        //    var toLowerExp = Expression.Call(property, toLowerMethod); // Equivalent to c.Property.ToLower()

        //    // 3. Apply the specific Filter Operator
        //    Expression operation;
        //    switch (expr.FilterOperator)
        //    {
        //        case "Equals":
        //            operation = Expression.Equal(toLowerExp, searchConstant);
        //            break;
        //        case "Not Equals":
        //            operation = Expression.NotEqual(toLowerExp, searchConstant);
        //            break;
        //        case "Starts With":
        //            operation = Expression.Call(toLowerExp, startsWithMethod, searchConstant);
        //            break;
        //        case "Ends With":
        //            operation = Expression.Call(toLowerExp, endsWithMethod, searchConstant);
        //            break;
        //        case "Not Contains":
        //            operation = Expression.Not(Expression.Call(toLowerExp, containsMethod, searchConstant));
        //            break;
        //        case "Contains":
        //        default:
        //            operation = Expression.Call(toLowerExp, containsMethod, searchConstant);
        //            break;
        //    }

        //    // 4. Combine Null Check with the Operation (e.g. c.Property != null && c.Property.Contains("x"))
        //    return Expression.AndAlso(nullCheck, operation);
        //}
        private Expression BuildCondition(ParameterExpression param, FilterCondition expr)
        {
            if (string.IsNullOrWhiteSpace(expr.SearchValue)) return null;
            string val = expr.SearchValue.Trim();

            string col = expr.SelectedColumn.ToLower();

            Expression property = null;
            Expression nullCheck = null;
            Type propType = typeof(string);

            // 1. Resolve Property and Target Type based on the column name
            switch (col)
            {
                case "complaint_id":
                    if (int.TryParse(val, out int idVal))
                    {
                        property = Expression.Property(param, "Complaint_id");
                        return expr.FilterOperator == "Not Equals"
                            ? Expression.NotEqual(property, Expression.Constant(idVal))
                            : Expression.Equal(property, Expression.Constant(idVal));
                    }
                    return null;

                case "client_code":
                    property = Expression.Property(param, "Client_code");
                    nullCheck = Expression.NotEqual(property, Expression.Constant(null, typeof(string)));
                    break;
                case "project_code":
                    property = Expression.Property(param, "Project_code");
                    nullCheck = Expression.NotEqual(property, Expression.Constant(null, typeof(string)));
                    break;
                case "issue_description":
                    property = Expression.Property(param, "Issue_description");
                    nullCheck = Expression.NotEqual(property, Expression.Constant(null, typeof(string)));
                    break;
                case "issue_raised_by":
                    property = Expression.Property(param, "Issue_raised_by");
                    nullCheck = Expression.NotEqual(property, Expression.Constant(null, typeof(string)));
                    break;
                case "project_name":
                    var clientProject = Expression.Property(param, "ClientProject");
                    property = Expression.Property(clientProject, "Project_name");
                    var navCheck = Expression.NotEqual(clientProject, Expression.Constant(null));
                    var propCheck = Expression.NotEqual(property, Expression.Constant(null, typeof(string)));
                    nullCheck = Expression.AndAlso(navCheck, propCheck);
                    break;

                // --- DATE COLUMNS ---
                case "project_start_date":
                    property = Expression.Property(param, "Project_start_date");
                    propType = typeof(DateTime?);
                    break;
                case "project_completion_date":
                    property = Expression.Property(param, "Project_completion_date");
                    propType = typeof(DateTime?);
                    break;
                case "amc_start_date":
                case "amc_start_dt":
                    property = Expression.Property(param, "Amc_start_date");
                    propType = typeof(DateTime?);
                    break;
                case "amc_finish_date":
                case "amc_end_dt":
                    property = Expression.Property(param, "Amc_finish_date");
                    propType = typeof(DateTime?);
                    break;
                case "issue_booked_date":
                    property = Expression.Property(param, "Issue_booked_date");
                    propType = typeof(DateTime?);
                    break;
                case "issue_allotted_date":
                    property = Expression.Property(param, "Issue_allotted_date");
                    propType = typeof(DateTime?);
                    break;
                case "issue_closed_date":
                    property = Expression.Property(param, "Issue_closed_date");
                    propType = typeof(DateTime?);
                    break;
                case "inserted_dt":
                    property = Expression.Property(param, "Inserted_dt");
                    propType = typeof(DateTime?);
                    break;
                case "start_dt":
                    var cpStart = Expression.Property(param, "ClientProject");
                    property = Expression.Property(cpStart, "Start_dt");
                    nullCheck = Expression.NotEqual(cpStart, Expression.Constant(null));
                    propType = typeof(DateTime?);
                    break;
                case "completed_dt":
                    var cpComp = Expression.Property(param, "ClientProject");
                    property = Expression.Property(cpComp, "Completed_dt");
                    nullCheck = Expression.NotEqual(cpComp, Expression.Constant(null));
                    propType = typeof(DateTime?);
                    break;

                default:
                    return null;
            }

            // 2. Handle DATE Expression Trees
            // Inside BuildCondition method in AuthController.cs (Date processing section):

            if (propType == typeof(DateTime) || propType == typeof(DateTime?))
            {
                Expression dateProp = property;
                Expression dateHasValueCheck = null;

                if (Nullable.GetUnderlyingType(property.Type) != null)
                {
                    dateHasValueCheck = Expression.Property(property, "HasValue");
                    dateProp = Expression.Property(property, "Value");
                }

                Expression dateOperation = null;

                // --- HANDLE ATOMIC IN RANGE ---
                if (expr.FilterOperator == "In Range")
                {
                    if (DateTime.TryParse(expr.SearchValue, out DateTime rangeStart) &&
                        DateTime.TryParse(expr.SearchValueTo, out DateTime rangeEnd))
                    {
                        DateTime startBound = rangeStart.Date;
                        DateTime endBound = rangeEnd.Date.AddDays(1); // Full day coverage

                        Expression gteRange = Expression.GreaterThanOrEqual(dateProp, Expression.Constant(startBound, typeof(DateTime)));
                        Expression ltRange = Expression.LessThan(dateProp, Expression.Constant(endBound, typeof(DateTime)));

                        dateOperation = Expression.AndAlso(gteRange, ltRange);
                    }
                }
                else if (DateTime.TryParse(val, out DateTime parsedDate))
                {
                    DateTime dayStart = parsedDate.Date;
                    DateTime dayEnd = dayStart.AddDays(1);
                    Expression dayStartConst = Expression.Constant(dayStart, typeof(DateTime));
                    Expression dayEndConst = Expression.Constant(dayEnd, typeof(DateTime));

                    switch (expr.FilterOperator)
                    {
                        case "Equals":
                            dateOperation = Expression.AndAlso(
                                Expression.GreaterThanOrEqual(dateProp, dayStartConst),
                                Expression.LessThan(dateProp, dayEndConst)
                            );
                            break;

                        case "Not Equals":
                            dateOperation = Expression.Not(Expression.AndAlso(
                                Expression.GreaterThanOrEqual(dateProp, dayStartConst),
                                Expression.LessThan(dateProp, dayEndConst)
                            ));
                            break;

                        case "Greater Than":
                            dateOperation = Expression.GreaterThanOrEqual(dateProp, dayEndConst);
                            break;

                        case "Greater Than Or Equal":
                            dateOperation = Expression.GreaterThanOrEqual(dateProp, dayStartConst);
                            break;

                        case "Less Than":
                            dateOperation = Expression.LessThan(dateProp, dayStartConst);
                            break;

                        case "Less Than Or Equal":
                            dateOperation = Expression.LessThan(dateProp, dayEndConst);
                            break;

                        default:
                            dateOperation = Expression.GreaterThanOrEqual(dateProp, dayStartConst);
                            break;
                    }
                }

                if (dateOperation == null) return null;

                Expression fullDateExpr = dateHasValueCheck != null
                    ? Expression.AndAlso(dateHasValueCheck, dateOperation)
                    : dateOperation;

                return nullCheck != null ? Expression.AndAlso(nullCheck, fullDateExpr) : fullDateExpr;
            }
            //if (propType == typeof(DateTime) || propType == typeof(DateTime?))
            //{
            //    if (!DateTime.TryParse(val, out DateTime parsedDate)) return null;

            //    Expression dateProp = property;
            //    Expression dateHasValueCheck = null;

            //    // Unwrap Nullable<DateTime> safely for EF Core queries
            //    if (Nullable.GetUnderlyingType(property.Type) != null)
            //    {
            //        dateHasValueCheck = Expression.Property(property, "HasValue");
            //        dateProp = Expression.Property(property, "Value");
            //    }

            //    // Calculate whole-day boundaries to match database timestamps seamlessly
            //    DateTime dayStart = parsedDate.Date;
            //    DateTime dayEnd = dayStart.AddDays(1);
            //    Expression dayStartConst = Expression.Constant(dayStart, typeof(DateTime));
            //    Expression dayEndConst = Expression.Constant(dayEnd, typeof(DateTime));

            //    Expression dateOperation;

            //    switch (expr.FilterOperator)
            //    {
            //        case "Equals":
            //            // c.Date >= dayStart AND c.Date < dayEnd
            //            var gte = Expression.GreaterThanOrEqual(dateProp, dayStartConst);
            //            var lt = Expression.LessThan(dateProp, dayEndConst);
            //            dateOperation = Expression.AndAlso(gte, lt);
            //            break;

            //        case "Not Equals":
            //            // !(c.Date >= dayStart AND c.Date < dayEnd)
            //            var gteN = Expression.GreaterThanOrEqual(dateProp, dayStartConst);
            //            var ltN = Expression.LessThan(dateProp, dayEndConst);
            //            dateOperation = Expression.Not(Expression.AndAlso(gteN, ltN));
            //            break;

            //        case "Greater Than":
            //            // c.Date >= dayEnd (strictly after this entire day)
            //            dateOperation = Expression.GreaterThanOrEqual(dateProp, dayEndConst);
            //            break;

            //        case "Greater Than Or Equal":
            //            // c.Date >= dayStart
            //            dateOperation = Expression.GreaterThanOrEqual(dateProp, dayStartConst);
            //            break;

            //        case "Less Than":
            //            // c.Date < dayStart
            //            dateOperation = Expression.LessThan(dateProp, dayStartConst);
            //            break;

            //        case "Less Than Or Equal":
            //            // c.Date < dayEnd (includes up to 23:59:59 of this day)
            //            dateOperation = Expression.LessThan(dateProp, dayEndConst);
            //            break;

            //        default:
            //            dateOperation = Expression.GreaterThanOrEqual(dateProp, dayStartConst);
            //            break;
            //    }

            //    Expression fullDateExpr = dateHasValueCheck != null
            //        ? Expression.AndAlso(dateHasValueCheck, dateOperation)
            //        : dateOperation;

            //    return nullCheck != null ? Expression.AndAlso(nullCheck, fullDateExpr) : fullDateExpr;
            //}

            // 3. Handle STRING Expression Trees
            val = val.ToLower();
            var toLowerMethod = typeof(string).GetMethod("ToLower", Type.EmptyTypes);
            var containsMethod = typeof(string).GetMethod("Contains", new[] { typeof(string) });
            var startsWithMethod = typeof(string).GetMethod("StartsWith", new[] { typeof(string) });
            var endsWithMethod = typeof(string).GetMethod("EndsWith", new[] { typeof(string) });

            var searchConstant = Expression.Constant(val);
            var toLowerExp = Expression.Call(property, toLowerMethod);

            Expression operation;
            switch (expr.FilterOperator)
            {
                case "Equals":
                    operation = Expression.Equal(toLowerExp, searchConstant);
                    break;
                case "Not Equals":
                    operation = Expression.NotEqual(toLowerExp, searchConstant);
                    break;
                case "Starts With":
                    operation = Expression.Call(toLowerExp, startsWithMethod, searchConstant);
                    break;
                case "Ends With":
                    operation = Expression.Call(toLowerExp, endsWithMethod, searchConstant);
                    break;
                case "Not Contains":
                    operation = Expression.Not(Expression.Call(toLowerExp, containsMethod, searchConstant));
                    break;
                case "Contains":
                default:
                    operation = Expression.Call(toLowerExp, containsMethod, searchConstant);
                    break;
            }

            return nullCheck != null ? Expression.AndAlso(nullCheck, operation) : operation;
        }
