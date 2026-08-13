hello,
i have made a server side MAUI hybrid web ag grid without enterprise. 

1.add this code to your razor
```
<div @ref="myGrid" class="ag-theme-balham" style="height:500px;position:relative; width:100%;" id="myGridId"></div>

    <div class="ag-paging-panel ag-unselectable ag-theme-balham" id="ag-25" style="border:1px solid darkgray;border-top: 0px !important;">
        <span class="ag-paging-row-summary-panel" role="status">
            <span id="ag-25-first-row" ref="lbFirstRowOnPage" class="ag-paging-row-summary-panel-number">@(TotalRecords == 0 ? 0 : PageSize * (_CurrentPage - 1) + 1)</span>
            <span id="ag-25-to">to</span>
                <span id="ag-25-last-row" ref="lbLastRowOnPage" class="ag-paging-row-summary-panel-number">@(Math.Min(_CurrentPage * PageSize, TotalRecords))</span>
            <span id="ag-25-of">of</span>
            <span id="ag-25-row-count" ref="lbRecordCount" class="ag-paging-row-summary-panel-number">@TotalRecords</span>
        </span>
        <span class="ag-paging-page-summary-panel" role="presentation">
            <div ref="btFirst" @onclick="GoFirst" disabled="@(_CurrentPage == 1 || PageSize == -1)" class="ag-paging-button @((_CurrentPage == 1 || PageSize == -1) ? "ag-disabled" : "")" role="button" aria-label="First Page" aria-disabled="true"><span class="ag-icon ag-icon-first" unselectable="on" role="presentation"></span></div>
            <div ref="btPrevious" @onclick="GoPrev" disabled="@(_CurrentPage == 1 || PageSize == -1)" class="ag-paging-button @((_CurrentPage == 1 || PageSize == -1) ? "ag-disabled" : "")" role="button" aria-label="Previous Page" aria-disabled="true"><span class="ag-icon ag-icon-previous" unselectable="on" role="presentation"></span></div>
            <span class="ag-paging-description" role="status">
                <span id="ag-25-start-page">Page</span>
                <span id="ag-25-start-page-number" ref="lbCurrent" class="ag-paging-number">@_CurrentPage</span>
                <span id="ag-25-of-page">of</span>
                <span id="ag-25-of-page-number" ref="lbTotal" class="ag-paging-number">@TotalPages</span>
            </span>
            <div ref="btNext" @onclick="GoNext" disabled="@(_CurrentPage >= TotalPages || PageSize == -1)" class="ag-paging-button @((_CurrentPage >= TotalPages || PageSize == -1) ? "ag-disabled" : "")" role="button" aria-label="Next Page" aria-disabled="true"><span class="ag-icon ag-icon-next" unselectable="on" role="presentation"></span></div>
            <div ref="btLast" @onclick="GoLast" disabled="@(_CurrentPage >= TotalPages || PageSize == -1)" class="ag-paging-button @((_CurrentPage >= TotalPages || PageSize == -1) ? "ag-disabled" : "")" role="button" aria-label="Last Page" aria-disabled="true"><span class="ag-icon ag-icon-last" unselectable="on" role="presentation"></span></div>
        </span>
    </div>
```
2.add all wwwroot files to the wwwroot of your shared folder. 
3.add the reference at top of the razor 
```
<script src="_content/Employee_Client.Shared/aggrid/ag-grid-community.min.js"></script>
<link rel="stylesheet" href="_content/Employee_Client.Shared/aggrid/ag-grid.css">
<link rel="stylesheet" href="_content/Employee_Client.Shared/aggrid/ag-theme-balham.css">
<script src="_content/AgGrid.Blazor/blazor-ag-grid.js"></script>

<script type="text/javascript" src="_content/Employee_Client.Shared/jsshared/alrt.js"></script>
<link rel="stylesheet" href="_content/Employee_Client.Shared/aggrid/ag-grid-cvspl.css">
<script type="text/javascript" src="_content/Employee_Client.Shared/jsshared/aggridh.js"></script>
```
(these files are from wwwroot of shared project)

4. add the controller files codes into your AuthController or your Controller in your (core-web-)api project, and create service methods to hit the endpoints of get-pagianted ( ```[HttpGet("complaints/paginated/{compCode}")]```.
now the ag grid will be created in razor with server side events of sorting and filtering.
please visit my website blog if you have time https://www.algolassi.online

warm regards
