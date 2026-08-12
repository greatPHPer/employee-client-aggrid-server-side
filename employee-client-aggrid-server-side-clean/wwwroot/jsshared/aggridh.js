window.agGridInterop = window.agGridInterop || {};
window.currentGridSearchTerm = "";
window.currentGridSearchFilters = []; // Store active column filters array
// Use this function from Blazor to update the search term
//window.updateSearchtext = function (a) {
//    window.currentGridSearchTerm = a || "";
//    if (window._myGridApi) {
//        window._myGridApi.refreshCells({
//            force: true
//        });
//    }
//};
// Updated function to parse incoming structured search data
window.updateSearchtext = function (filtersJson) {
    //console.log('fj:',filtersJson);
    try {
        window.currentGridSearchFilters = JSON.parse(filtersJson || "[]");
    } catch (e) {
        window.currentGridSearchFilters = [];
    }

    if (window._myGridApi) {
        window._myGridApi.refreshCells({ force: true });
    }
};

// Define the Highlighter Class
// Define the Smart Column-Specific Highlighter Class
//if (!window.HighlightCellRenderer) {
//    window.HighlightCellRenderer = class {
//        init(params) {
//            this.eGui = document.createElement('span');
//            let value = params.value != null ? String(params.value) : '';
//            if (!value) return;

//            // Find the active search terms target matching THIS current cell's column ID
//            let currentColId = params.column.getColId();

//            let matchingFilters = (window.currentGridSearchFilters || [])
//                .filter(f => f.col && f.col.toLowerCase() === currentColId.toLowerCase() && f.val);

//            if (matchingFilters.length > 0) {
//                try {
//                    // Collect and escape search text targeted only to this column
//                    let terms = matchingFilters.map(f => f.val);
//                    let escapedTerms = terms.map(t => t.replace(/[.*+?^${}()|[\\]\\\\]/g, '\\$&'));

//                    let regexPattern = '(' + escapedTerms.join('|') + ')';
//                    let regex = new RegExp(regexPattern, 'ig');

//                    // Highlight matches strictly within this column
//                    this.eGui.innerHTML = value.replace(regex, '<span style="background-color:yellow;color:black;">$&</span>');
//                } catch (e) {
//                    this.eGui.innerText = value;
//                }
//            } else {
//                // No matching filter for this specific column, display normal text
//                this.eGui.innerText = value;
//            }
//        }
//        getGui() { return this.eGui; }
//    }
//}
if (!window.HighlightCellRenderer) {
    window.HighlightCellRenderer = class {
        init(params) {
            this.eGui = document.createElement('span');

            // --- RECTIFICATION HERE ---
            // Use params.valueFormatted if available (from valueFormatter), else fallback to raw params.value
            let displayValue = params.valueFormatted != null ? params.valueFormatted : params.value;
            let value = displayValue != null ? String(displayValue) : '';

            if (!value) return;

            let currentColId = String(params.column.getColId()).toLowerCase();

            let matchingFilters = (window.currentGridSearchFilters || [])
                .filter(f => f.col && String(f.col).toLowerCase() === currentColId && f.val);

            if (matchingFilters.length > 0) {
                try {
                    let terms = matchingFilters.map(f => f.val);
                    let escapedTerms = terms.map(t => t.replace(/[.*+?^${}()|[\]\\]/g, '\\$&'));
                    let regexPattern = '(' + escapedTerms.join('|') + ')';
                    let regex = new RegExp(regexPattern, 'ig');

                    this.eGui.innerHTML = value.replace(regex, '<span style="background-color:yellow;color:black;">$&</span>');
                } catch (e) {
                    this.eGui.innerText = value;
                }
            } else {
                this.eGui.innerText = value;
            }
        }
        getGui() { return this.eGui; }
    };
}
// create once, reuse later
//window.agGridInterop.createOrReuseGrid = function (element, columnDefs, rowData, dotNetRef) {
//    if (!element) return;

//    // 1. Define Grid Options with the registered component
//    var gridOptions = {
//        columnDefs: columnDefs,
//        rowData: rowData,
//        components: {
//            HighlightCellRenderer: HighlightCellRenderer // <--- Registration
//        },
//        defaultColDef: { sortable: true, filter: 'agTextColumnFilter', resizable: true }
//    };

//    // 2. If grid exists, update it
//    if (window._myGridApi) {
//        window._myGridApi.setGridOption('columnDefs', columnDefs);
//        window._myGridApi.setGridOption('rowData', rowData);
//        return;
//    }

//    // 3. Create grid if it doesn't exist
//    window._myGridApi = new agGrid.Grid(element, gridOptions);
//};
// Add a flag to prevent infinite loops
window.isPaginationChanging = false;
// create once, reuse later
window.formatDate = function (params) {
    //console.log(params.value);
    if (!params.value) return '';
    const date = new Date(params.value);
    const map = {
        'dd': String(date.getDate()).padStart(2, '0'),
        'MM': String(date.getMonth() + 1).padStart(2, '0'),
        'MMM': date.toLocaleString('default', { month: 'short' }),
        'yyyy': date.getFullYear()
    };
    //console.log(window.currentDateFormat.replace(/dd|MM|MMM|yyyy/gi, matched => map[matched]));
    return window.currentDateFormat.replace(/dd|MM|MMM|yyyy/gi, matched => map[matched]);
}
window.agGridInterop.createOrReuseGrid = function (element, columnDefs, rowData, dotNetRef, dateFormat) {
    if (!element) {
        console.error('createOrReuseGrid: element is null');
        return;
    }
    // --- RECTIFICATION: Map string function names to actual JS functions ---
    columnDefs = processColumnDefs(columnDefs);
    // Save the C# date format globally in JS
    if (dateFormat) {
        window.currentDateFormat = dateFormat;
    }
    if (columnDefs) {
        if (columnDefs.defaultColDef) {
            columnDefs.defaultColDef.resizable = true;
        }
        else {
            columnDefs.defaultColDef = { sortable: true, filter: 'agTextColumnFilter', resizable: true };
        }
    }
    // if grid already exists, just update data
    if (window._myGridApi) {
        if (columnDefs) window._myGridApi.setColumnDefs(columnDefs);
        if (rowData) window._myGridApi.setRowData(rowData);
        return;
    }
    

    function sizeToFit(api) {
        // Get the top position of the grid
        const gridDiv = document.querySelector('#myGridId');
        if (!gridDiv)
            return;
        const offsetTop = gridDiv.offsetTop;

        // Calculate remaining height to window bottom (minus some padding)
        var newHeight = window.innerHeight - offsetTop - 20;
        newHeight = Math.max(newHeight, 300);
        // Apply height
        gridDiv.style.height = `${newHeight}px`;

        // Inform AG Grid to resize
        api.sizeColumnsToFit();
    }

    const gridOptions = {
        enableCharts: true,
        enableRangeSelection: true,
        //pagination: false,

        onGridReady: function (params) {
            //window._myGridApi = params.api;

            // ADD THIS: Save the columnApi specifically for v29
            window._myGridColumnApi = params.columnApi;
        },






        onFirstDataRendered: onFirstDataRendered,
        immutableData: true,
        animateRows: true,
        getRowNodeId: function (params) { return params.data.complaint_id; },
        getRowId: function (params) { return params.data.complaint_id; },
        columnDefs: columnDefs || [],
        rowData: rowData || [],
        defaultColDef: { sortable: true, filter: 'agTextColumnFilter', resizable: true },
        rowSelection: 'single',
        suppressRowClickSelection: true,
        onRowClicked: function (params) {
            params.node.setSelected(!params.node.isSelected(), true);
        },
        onSelectionChanged: function (event) {
            const rows = event.api.getSelectedRows();
            dotNetRef.invokeMethodAsync('OnSelectionChanged', rows).catch(console.error);
        },
        // ADD THIS LINE BELOW
        components: {
            HighlightCellRenderer: HighlightCellRenderer
        },
        //onSortChanged: function (event) {
        //    // 1. Get the full column state from the columnApi
        //    const columnState = event.columnApi.getColumnState();

        //    // 2. Filter out columns that aren't sorted, then map to your string format
        //    const activeSorts = columnState
        //        .filter(s => s.sort != null)
        //        .map(s => `${s.colId} ${s.sort}`);

        //    // 3. Pass the string array to Blazor
        //    if (dotNetRef) {
        //        dotNetRef.invokeMethodAsync('LoadGridDataAsync', activeSorts,null).catch(console.error);
        //    }
        //},
        onSortChanged: function (event) {
            // 1. Get column state safely (supports v29 via event.columnApi and v31+ via event.api)
            const columnApi = event.columnApi || event.api;
            const columnState = columnApi.getColumnState();

            // 2. Filter active sorts
            const activeSorts = columnState
                .filter(s => s.sort != null)
                .map(s => `${s.colId} ${s.sort}`);

            // 3. Extract active filters from Grid API
            const filterModel = event.api.getFilterModel();
            const activeFilters = [];

            const mapOperator = (agType) => {
                switch (agType) {
                    case 'equals': return 'Equals';
                    case 'notEqual': return 'Not Equals';
                    case 'greaterThan': return 'Greater Than';
                    case 'lessThan': return 'Less Than';
                    case 'greaterThanOrEqual': return 'Greater Than Or Equal';
                    case 'lessThanOrEqual': return 'Less Than Or Equal';
                    case 'notContains': return 'Not Contains';
                    case 'startsWith': return 'Starts With';
                    case 'endsWith': return 'Ends With';
                    case 'contains':
                    default: return 'Contains';
                }
            };

            const parseCondition = (colId, cond, nextOp) => {
                if (!cond) return null;

                if (cond.filterType === 'date' && cond.type === 'inRange' && cond.dateFrom && cond.dateTo) {
                    return {
                        SelectedColumn: colId,
                        SearchValue: String(cond.dateFrom),
                        SearchValueTo: String(cond.dateTo),
                        FilterOperator: "In Range",
                        NextLogicalOperator: nextOp
                    };
                }

                let rawVal = cond.filter !== undefined ? cond.filter : cond.dateFrom;
                if (rawVal !== undefined && rawVal !== null && rawVal !== '') {
                    return {
                        SelectedColumn: colId,
                        SearchValue: String(rawVal),
                        FilterOperator: mapOperator(cond.type),
                        NextLogicalOperator: nextOp
                    };
                }
                return null;
            };

            for (const colId in filterModel) {
                if (filterModel.hasOwnProperty(colId)) {
                    const f = filterModel[colId];

                    if (f.operator && f.condition1 && f.condition2) {
                        const operatorLogic = f.operator.toUpperCase();
                        const c1 = parseCondition(colId, f.condition1, operatorLogic);
                        const c2 = parseCondition(colId, f.condition2, "AND");

                        if (c1) activeFilters.push(c1);
                        if (c2) activeFilters.push(c2);
                    } else {
                        const c = parseCondition(colId, f, "AND");
                        if (c) activeFilters.push(c);
                    }
                }
            }

            // 4. Send active sorts AND filters to Blazor
            if (dotNetRef) {
                dotNetRef.invokeMethodAsync('LoadGridDataAsync', activeSorts, JSON.stringify(activeFilters))
                    .catch(console.error);
            }
        },
        // --- ADD THIS NEW FILTER EVENT ---

        onFilterChanged: function (event) {
            // 1. Grab current sorts
            const columnState = event.columnApi ? event.columnApi.getColumnState() : event.api.getColumnState();
            const activeSorts = columnState
                .filter(s => s.sort != null)
                .map(s => `${s.colId} ${s.sort}`);

            // 2. Extract AG Grid's internal filter model
            const filterModel = event.api.getFilterModel();
            const activeFilters = [];

            // Helper to map AG Grid operators
            const mapOperator = (agType) => {
                switch (agType) {
                    case 'equals': return 'Equals';
                    case 'notEqual': return 'Not Equals';
                    case 'greaterThan': return 'Greater Than';
                    case 'lessThan': return 'Less Than';
                    case 'greaterThanOrEqual': return 'Greater Than Or Equal';
                    case 'lessThanOrEqual': return 'Less Than Or Equal';
                    case 'notContains': return 'Not Contains';
                    case 'startsWith': return 'Starts With';
                    case 'endsWith': return 'Ends With';
                    case 'contains':
                    default: return 'Contains';
                }
            };

            // Helper to extract a condition (atomic handling for In Range)
            const parseCondition = (colId, cond, nextOp) => {
                if (!cond) return null;

                // Atomic In Range Condition
                if (cond.filterType === 'date' && cond.type === 'inRange' && cond.dateFrom && cond.dateTo) {
                    return {
                        SelectedColumn: colId,
                        SearchValue: String(cond.dateFrom),
                        SearchValueTo: String(cond.dateTo),
                        FilterOperator: "In Range",
                        NextLogicalOperator: nextOp
                    };
                }

                // Standard Single Condition (Text, Number, Single Date)
                let rawVal = cond.filter !== undefined ? cond.filter : cond.dateFrom;
                if (rawVal !== undefined && rawVal !== null && rawVal !== '') {
                    return {
                        SelectedColumn: colId,
                        SearchValue: String(rawVal),
                        FilterOperator: mapOperator(cond.type),
                        NextLogicalOperator: nextOp
                    };
                }
                return null;
            };

            // 3. Parse active filter model & bypass AG Grid client-side row hiding
            for (const colId in filterModel) {
                if (filterModel.hasOwnProperty(colId)) {
                    const f = filterModel[colId];

                    // Prevent AG Grid from suppressing rows client-side after SQL returns them
                    if (typeof event.api.getFilterInstance === 'function') {
                        event.api.getFilterInstance(colId, function (instance) {
                            if (instance) {
                                instance.doesFilterPass = function () { return true; };
                            }
                        });
                    }

                    // CASE A: Two conditions with AND / OR
                    if (f.operator && f.condition1 && f.condition2) {
                        const operatorLogic = f.operator.toUpperCase(); // "OR" or "AND"
                        const c1 = parseCondition(colId, f.condition1, operatorLogic);
                        const c2 = parseCondition(colId, f.condition2, "AND");

                        if (c1) activeFilters.push(c1);
                        if (c2) activeFilters.push(c2);
                    }
                    // CASE B: Single condition
                    else {
                        const c = parseCondition(colId, f, "AND");
                        if (c) activeFilters.push(c);
                    }
                }
            }

            // 4. Send to Blazor
            if (dotNetRef) {
                dotNetRef.invokeMethodAsync('LoadGridDataAsync', activeSorts, JSON.stringify(activeFilters))
                    .catch(console.error);
            }
        },
        //onFilterChanged: function (event) {
        //    // 1. Grab current sorts so we don't lose sorting when filtering
        //    const columnState = event.columnApi ? event.columnApi.getColumnState() : event.api.getColumnState();
        //    const activeSorts = columnState
        //        .filter(s => s.sort != null)
        //        .map(s => `${s.colId} ${s.sort}`);

        //    // 2. Extract AG Grid's internal filter model
        //    const filterModel = event.api.getFilterModel();
        //    const activeFilters = [];

        //    // Helper to map AG Grid filter types to your C# FilterOperators
        //    const mapOperator = (agType) => {
        //        switch (agType) {
        //            case 'equals': return 'Equals';
        //            case 'notEqual': return 'Not Equals';
        //            case 'greaterThan': return 'Greater Than';
        //            case 'lessThan': return 'Less Than';
        //            case 'greaterThanOrEqual': return 'Greater Than Or Equal';
        //            case 'lessThanOrEqual': return 'Less Than Or Equal';
        //            case 'notContains': return 'Not Contains';
        //            case 'startsWith': return 'Starts With';
        //            case 'endsWith': return 'Ends With';
        //            case 'contains':
        //            default: return 'Contains';
        //        }
        //    };

        //    // Helper function to extract value and operator from a single condition (Text, Number, or Date)
        //    const processCondition = (colId, cond, defaultNextOperator = "AND") => {
        //        if (!cond) return;

        //        // CASE A: Date Filter "In Range" (Between Date A and Date B)
        //        if (cond.filterType === 'date' && cond.type === 'inRange' && cond.dateFrom && cond.dateTo) {
        //            activeFilters.push({
        //                SelectedColumn: colId,
        //                SearchValue: String(cond.dateFrom),
        //                FilterOperator: "Greater Than Or Equal",
        //                NextLogicalOperator: "AND"
        //            });
        //            activeFilters.push({
        //                SelectedColumn: colId,
        //                SearchValue: String(cond.dateTo),
        //                FilterOperator: "Less Than Or Equal",
        //                NextLogicalOperator: defaultNextOperator
        //            });
        //            return;
        //        }

        //        // CASE B: Standard Text/Number (cond.filter) or Single Date (cond.dateFrom)
        //        let rawVal = cond.filter !== undefined ? cond.filter : cond.dateFrom;

        //        if (rawVal !== undefined && rawVal !== null && rawVal !== '') {
        //            activeFilters.push({
        //                SelectedColumn: colId,
        //                SearchValue: String(rawVal),
        //                FilterOperator: mapOperator(cond.type),
        //                NextLogicalOperator: defaultNextOperator
        //            });
        //        }
        //    };

        //    // 3. Parse the dynamic filter model
        //    for (const colId in filterModel) {
        //        if (filterModel.hasOwnProperty(colId)) {
        //            const f = filterModel[colId];

        //            // CASE 1: Two conditions on the same column (AND / OR)
        //            if (f.operator && f.condition1 && f.condition2) {
        //                const nextOp = f.operator.toUpperCase(); // e.g., "AND" or "OR"
        //                processCondition(colId, f.condition1, nextOp);
        //                processCondition(colId, f.condition2, "AND");
        //            }
        //            // CASE 2: Single condition on the column
        //            else {
        //                processCondition(colId, f, "AND");
        //            }
        //        }
        //    }

        //    console.log('Parsed activeFilters for C#:', activeFilters);

        //    // 4. Send to Blazor
        //    if (dotNetRef) {
        //    //dotNetRef.invokeMethodAsync('LoadGridDataAsync', activeSorts, JSON.stringify(activeFilters))
        //        dotNetRef.invokeMethodAsync('LoadGridDataAsync', activeSorts, JSON.stringify(activeFilters))
        //            .catch(console.error);
        //    }
        //},
        //onFilterChanged: function (event) {
        //    // 1. Grab current sorts so we don't lose sorting when filtering
        //    const columnState = event.columnApi ? event.columnApi.getColumnState() : event.api.getColumnState();
        //    const activeSorts = columnState
        //        .filter(s => s.sort != null)
        //        .map(s => `${s.colId} ${s.sort}`);

        //    // 2. Extract AG Grid's internal filter model
        //    const filterModel = event.api.getFilterModel();
        //    const activeFilters = [];

        //    // 3. Map it to your C# FilterCondition structure
        //    //for (const colId in filterModel) {
        //    //    if (filterModel.hasOwnProperty(colId)) {
        //    //        const f = filterModel[colId];
        //    //        if (f.filter) {
        //    //            activeFilters.push({
        //    //                SelectedColumn: colId,
        //    //                SearchValue: String(f.filter),
        //    //                FilterOperator: "Contains", // Defaulting to Contains
        //    //                NextLogicalOperator: "AND"
        //    //            });
        //    //        }
        //    //    }
        //    //}
        //    // Helper to map AG Grid types to your C# FilterOperators
        //    const mapOperator = (agType) => {
        //        switch (agType) {
        //            case 'equals': return 'Equals';
        //            case 'notEqual': return 'Not Equals';
        //            case 'notContains': return 'Not Contains';
        //            case 'startsWith': return 'Starts With';
        //            case 'endsWith': return 'Ends With';
        //            case 'contains':
        //            default: return 'Contains';
        //        }
        //    };

        //    // 3. Parse the dynamic filter model
        //    for (const colId in filterModel) {
        //        if (filterModel.hasOwnProperty(colId)) {
        //            const f = filterModel[colId];

        //            // CASE A: Two conditions on the same column (AND/OR)
        //            if (f.operator && f.condition1 && f.condition2) {

        //                // Push Condition 1
        //                activeFilters.push({
        //                    SelectedColumn: colId,
        //                    SearchValue: String(f.condition1.filter),
        //                    FilterOperator: mapOperator(f.condition1.type),
        //                    NextLogicalOperator: f.operator.toUpperCase() // e.g., "OR", "AND"
        //                });

        //                // Push Condition 2
        //                activeFilters.push({
        //                    SelectedColumn: colId,
        //                    SearchValue: String(f.condition2.filter),
        //                    FilterOperator: mapOperator(f.condition2.type),
        //                    NextLogicalOperator: "AND" // Bridge to the next column's filter
        //                });
        //            }
        //            // CASE B: Single condition on the column
        //            else if (f.filter !== undefined) {
        //                activeFilters.push({
        //                    SelectedColumn: colId,
        //                    SearchValue: String(f.filter),
        //                    FilterOperator: mapOperator(f.type),
        //                    NextLogicalOperator: "AND" // Bridge to the next column's filter
        //                });
        //            }
        //        }
        //    }
        //    console.log('af:', filterModel);
        //    // 4. Send to Blazor
        //    if (dotNetRef) {
        //        dotNetRef.invokeMethodAsync('LoadGridDataAsync', activeSorts, JSON.stringify(activeFilters))
        //            .catch(console.error);
        //    }
        //},
        pagination: false,             // Enable Native Pagination
        //paginationPageSize: 10,       // Matches your backend default
        //suppressPaginationPanel: false,

        //onPaginationChanged: function (params) {
        //    // Prevent the update loop
        //    if (window.isPaginationChanging) return;

        //    const api = params.api;
        //    const currentPage = api.paginationGetCurrentPage() + 1; // AG Grid is 0-indexed

        //    // Only trigger if the page actually changed
        //    if (dotNetRef) {
        //        window.isPaginationChanging = true;
        //        dotNetRef.invokeMethodAsync('OnPageChanged', currentPage)
        //            .then(() => { window.isPaginationChanging = false; })
        //            .catch(() => { window.isPaginationChanging = false; });
        //    }
        //},
        //onColumnVisible: () => window.addCustomHeaderMenuIcon(gridOptions),
        onColumnVisible: function (event) {
            // Keep your existing custom header icon logic[cite: 8]
            window.addCustomHeaderMenuIcon(gridOptions);

            // 1. Get column state safely (supports v29 via event.columnApi and v31+ via event.api)[cite: 8]
            const api = event.columnApi || event.api;
            const columnState = api.getColumnState();

            // 2. Send state to Blazor
            if (dotNetRef) {
                dotNetRef.invokeMethodAsync('SaveColumnStateAsync', JSON.stringify(columnState))
                    .catch(console.error);
            }
            window.addCustomHeaderMenuIcon(gridOptions)
        },
        onColumnResized: () => window.addCustomHeaderMenuIcon(gridOptions),
        onGridColumnsChanged: () => window.addCustomHeaderMenuIcon(gridOptions),
        onBodyScroll: () => window.addCustomHeaderMenuIcon(gridOptions),
    };
    function onFirstDataRendered(params) {
        sizeToFit(params.api);
        window.addEventListener('resize', () => sizeToFit(params.api));
        window.addCustomHeaderMenuIcon(gridOptions);
    }
    new agGrid.Grid(element, gridOptions);

    console.log("Grid API:", gridOptions.api);
    console.log("Element connected:", element.isConnected);
    // gridOptions.api is populated after creation
    window._myGridApi = gridOptions.api || null;
};
window.agGridInterop.applyColumnState = function (stateJson) {
    // Check for columnApi instead of the main API
    if (window._myGridColumnApi && stateJson) {
        try {
            const state = JSON.parse(stateJson);

            // Apply the state using the v29 columnApi
            window._myGridColumnApi.applyColumnState({
                state: state,
                applyOrder: true // Applies saved column ordering as well
            });

        } catch (e) {
            console.error("Failed to parse or apply column state", e);
        }
    } else {
        console.warn("ag-Grid columnApi is not initialized or state is empty.");
    }
};
// update row data later
window.agGridInterop.setRowData = function (rowData) {
    if (!window._myGridApi) {
        console.warn('setRowData: gridApi not available');
        return;
    }
    console.log("rs:",rowData);
    window._myGridApi.setRowData(rowData);
};

window.agGridInterop.setQuickFilter = function (SearchTerm) {
    if (!window._myGridApi) {
        console.warn('setRowData: gridApi not available');
        return;
    }
    window._myGridApi.setQuickFilter(SearchTerm);
};

// optional: destroy grid if you want to recreate later
window.agGridInterop.destroyGrid = function () {
    if (window._myGridApi) {
        try { window._myGridApi.destroy(); } catch (e) { console.warn(e); }
        window._myGridApi = null;
    }
};







//window.addCustomHeaderMenuIcon = function () {
//    // Select all the filter button icons currently in the DOM
//    const filterButtons = document.querySelectorAll('.ag-header-icon.ag-header-cell-filter-button');

//    filterButtons.forEach(btn => {
//        // Prevent duplicate spans if AG Grid triggers this multiple times
//        if (btn.previousElementSibling && btn.previousElementSibling.classList.contains('ag-header-cell-menu-button')) {
//            return;
//        }

//        // Create the outer span
//        const customSpan = document.createElement('span');
//        customSpan.className = 'ag-header-icon ag-header-cell-menu-button ag-header-menu-icon ag-header-menu-always-show';
//        customSpan.setAttribute('data-ref', 'eMenu');
//        customSpan.setAttribute('aria-hidden', 'true');

//        // Create the inner icon span
//        const innerSpan = document.createElement('span');
//        innerSpan.className = 'ag-icon ag-icon-menu-alt';
//        innerSpan.setAttribute('role', 'presentation');
//        innerSpan.setAttribute('unselectable', 'on');

//        customSpan.appendChild(innerSpan);

//        // Insert the custom span just before the filter button sibling
//        btn.parentNode.insertBefore(customSpan, btn);
//    });
//};
window.addCustomHeaderMenuIcon_oldold = function (gridOptions) {
    // Select all the header container wrappers in the DOM
    const containers = document.querySelectorAll('.ag-cell-label-container');

    containers.forEach(container => {
        // Prevent duplicate spans if AG Grid triggers this multiple times
        if (container.firstElementChild && container.firstElementChild.classList.contains('ag-header-cell-menu-button')) {
            //return;
        }

        // Create the outer span
        const customSpan = document.createElement('span');
        customSpan.className = 'ag-header-icon ag-header-cell-menu-button';
        // ag-header-menu-icon ag-header-menu-always-show
        customSpan.setAttribute('data-ref', 'eMenu');
        customSpan.setAttribute('aria-hidden', 'true');

        // Create the inner icon span
        const innerSpan = document.createElement('span');
        innerSpan.className = 'ag-icon ag-icon-columns';
        //innerSpan.className = 'ag-menu-option-part ag-menu-option-icon';ag-icon-menu-alt
        innerSpan.setAttribute('role', 'presentation');
        innerSpan.setAttribute('unselectable', 'on');




        customSpan.addEventListener('click', (event) => {
            //event.stopPropagation(); // Prevents the grid from sorting the column on click
            //event.preventDefault();

            // Pass the click event and your gridApi to the popup generator
            // Note: ensure 'gridApi' is accessible in this scope!

            renderDynamicColumnPanel(gridOptions.columnApi,event);
        });






        customSpan.appendChild(innerSpan);

        // Insert the custom span as the FIRST child of the container
        container.prepend(customSpan);
    });
};
window.addCustomHeaderMenuIcon = function (gridOptions) {
    const containers = document.querySelectorAll('.ag-cell-label-container');

    containers.forEach(container => {
        // IDEMPOTENT CHECK: If the icon already exists, skip it
        /*if (container.querySelector('.ag-header-cell-menu-button')) {*/
        if (container.querySelector('.ag-icon-columns')) {
            return;
        }

        const customSpan = document.createElement('span');
        customSpan.className = 'ag-header-icon ag-header-cell-menu-button';
        customSpan.setAttribute('data-ref', 'eMenu');
        customSpan.setAttribute('aria-hidden', 'true');

        const innerSpan = document.createElement('span');
        innerSpan.className = 'ag-icon ag-icon-columns';
        innerSpan.setAttribute('role', 'presentation');
        innerSpan.setAttribute('unselectable', 'on');

        customSpan.addEventListener('click', (event) => {
            renderDynamicColumnPanel(gridOptions.columnApi, event);
        });

        customSpan.appendChild(innerSpan);


        //container.prepend(customSpan);
        // Find internal elements
        const labelEl = container.querySelector('.ag-header-cell-label') || container.querySelector('[data-ref="eLabel"]');
        const filterBtn = container.querySelector('.ag-header-cell-filter-button') || container.querySelector('[data-ref="eFilter"]');

        // Set explicit flex ordering: Title (1) -> Column Icon (2) -> Filter Icon (3)
        if (labelEl) {
            labelEl.style.order = '2';
        }

        customSpan.style.order = '1';
        customSpan.style.marginLeft = 'auto'; // Pushes icons to the right end
        customSpan.style.marginRight = '4px';  // Spacing before filter icon

        if (filterBtn) {
            filterBtn.style.order = '3';
            filterBtn.style.marginLeft = '2px';
        }

        // Append custom icon to container
        container.appendChild(customSpan);
    });
};
//window.renderColumnChooserPopup = function (event, api, columnApi) {
//    const existingPopup = document.getElementById('custom-column-chooser');
//    if (existingPopup) {
//        existingPopup.remove();
//    }

//    const popup = document.createElement('div');
//    popup.id = 'custom-column-chooser';
//    popup.className = 'ag-custom-column-popup';
//    popup.style.top = `${event.clientY + 15}px`;
//    popup.style.left = `${event.clientX}px`;

//    const header = document.createElement('div');
//    header.className = 'ag-custom-popup-header';
//    header.innerHTML = `
//        <span>Choose Columns</span>
//        <button id="close-column-chooser" style="border:none; background:none; cursor:pointer;">✖</button>
//    `;
//    popup.appendChild(header);

//    const listContainer = document.createElement('div');
//    listContainer.className = 'ag-custom-popup-list';

//    // FIX: Use columnApi.getAllColumns() for AG Grid v30 and below
//    const columns = columnApi.getAllColumns();

//    columns.forEach(col => {
//        const colDef = col.getColDef();
//        const colId = col.getColId();
//        const isVisible = col.isVisible();

//        const displayName = colDef.headerName || colDef.field;
//        if (!displayName) return;

//        const listItem = document.createElement('label');
//        listItem.className = 'ag-custom-popup-item';

//        const checkbox = document.createElement('input');
//        checkbox.type = 'checkbox';
//        checkbox.checked = isVisible;

//        // FIX: Use columnApi to set column visibility 
//        checkbox.addEventListener('change', (e) => {
//            columnApi.setColumnVisible(colId, e.target.checked);
//        });

//        listItem.appendChild(checkbox);
//        listItem.appendChild(document.createTextNode(displayName));

//        listContainer.appendChild(listItem);
//    });

//    popup.appendChild(listContainer);
//    document.body.appendChild(popup);

//    document.getElementById('close-column-chooser').addEventListener('click', () => {
//        popup.remove();
//    });

//    setTimeout(() => {
//        const clickOutsideHandler = (e) => {
//            if (!popup.contains(e.target)) {
//                popup.remove();
//                document.removeEventListener('click', clickOutsideHandler);
//            }
//        };
//        document.addEventListener('click', clickOutsideHandler);
//    }, 0);
//};


window.renderDynamicColumnPanel_old = function (columnApi, event) {
    // 1. Helper function to create elements quickly
    const createEl = (tag, className, attributes = {}, styles = {}) => {
        const el = document.createElement(tag);
        if (className) el.className = className;
        for (let key in attributes) el.setAttribute(key, attributes[key]);
        for (let key in styles) el.style[key] = styles[key];
        return el;
    };

    // 2. Build the outer structure
    const root = createEl('div', 'ag-styled-root ag-theme-inherit-1');
    const popupWrapper = createEl('div', 'ag-popup');
    const panel = createEl('div', 'ag-panel ag-default-panel ag-dialog ag-popup-child ag-focus-managed',
        { 'role': 'dialog', 'aria-label': 'Choose Columns' },
        { 'position': 'absolute', 'top': `${event.clientY}px`, 'left': `${event.clientX}px`, 'width': '300px', 'height': '300px' }
    );

    // 3. Title Bar
    const titleBar = createEl('div', 'ag-panel-title-bar ag-default-panel-title-bar ag-unselectable');
    const title = createEl('span', 'ag-panel-title-bar-title');
    title.innerText = 'Choose Columns';
    const closeBtn = createEl('div', 'ag-button ag-panel-title-bar-button');
    const closeIcon = createEl('span', 'ag-icon ag-icon-cross');

    closeBtn.appendChild(closeIcon);
    titleBar.appendChild(title);
    titleBar.appendChild(closeBtn);

    // Close functionality
    closeBtn.onclick = () => root.remove();

    // 4. Content Wrapper & List
    const contentWrapper = createEl('div', 'ag-panel-content-wrapper ag-default-panel-content-wrapper');
    const listViewport = createEl('div', 'ag-virtual-list-viewport ag-column-select-virtual-list-viewport');
    const listContainer = createEl('div', 'ag-virtual-list-container ag-column-select-virtual-list-container', { 'role': 'tree' });

    // 5. Populate Columns Dynamically
    columnApi.getAllColumns().forEach((col, index) => {
        const colId = col.getColId();
        const colDef = col.getColDef();
        const isVisible = col.isVisible();

        const item = createEl('div', 'ag-virtual-list-item ag-column-select-virtual-list-item',
            { 'role': 'treeitem', 'aria-label': `${colDef.headerName} Column` }
        );
        const colWrapper = createEl('div', 'ag-column-select-column');

        // Checkbox structure
        const cbWrapper = createEl('div', 'ag-column-select-checkbox ag-checkbox ag-input-field');
        const inputWrapper = createEl('div', 'ag-wrapper ag-checkbox-input-wrapper');
        if (isVisible) inputWrapper.classList.add('ag-checked');

        const input = createEl('input', 'ag-input-field-input ag-checkbox-input',
            { 'type': 'checkbox' }
        );
        input.checked = isVisible;
        input.onchange = (e) => columnApi.setColumnVisible(colId, e.target.checked);

        const label = createEl('span', 'ag-column-select-column-label');
        label.innerText = colDef.headerName || colDef.field;

        // Assembly
        inputWrapper.appendChild(input);
        cbWrapper.appendChild(inputWrapper);
        colWrapper.appendChild(cbWrapper);
        colWrapper.appendChild(label);
        item.appendChild(colWrapper);
        listContainer.appendChild(item);
    });

    // 6. Assemble everything
    listViewport.appendChild(listContainer);
    contentWrapper.appendChild(listViewport);
    panel.appendChild(titleBar);
    panel.appendChild(contentWrapper);
    popupWrapper.appendChild(panel);
    root.appendChild(popupWrapper);

    // 7. Inject into DOM
    document.body.appendChild(root);
};

window.renderDynamicColumnPanel_old3 = function (columnApi, event) {
    // 1. Helper function to create elements
    const createEl = (tag, className, attributes = {}, styles = {}) => {
        const el = document.createElement(tag);
        if (className) el.className = className;
        for (let key in attributes) el.setAttribute(key, attributes[key]);
        for (let key in styles) el.style[key] = styles[key];
        return el;
    };

    // 2. Build the outer structure (same as before)
    const root = createEl('div', 'ag-styled-root ag-theme-inherit-1');
    const popupWrapper = createEl('div', 'ag-popup');
    const panel = createEl('div', 'ag-panel ag-default-panel ag-dialog ag-popup-child ag-focus-managed',
        { 'role': 'dialog', 'aria-label': 'Choose Columns' },
        { 'position': 'absolute', 'top': '10px', 'left': '10px', 'width': '300px', 'height': '300px', 'z-index': '1000' }
    );

    // 3. Title Bar
    const titleBar = createEl('div', 'ag-panel-title-bar ag-default-panel-title-bar ag-unselectable');
    const title = createEl('span', 'ag-panel-title-bar-title');
    title.innerText = 'Choose Columns';
    const closeBtn = createEl('div', 'ag-button ag-panel-title-bar-button');
    closeBtn.appendChild(createEl('span', 'ag-icon ag-icon-cross'));

    titleBar.appendChild(title);
    titleBar.appendChild(closeBtn);
    closeBtn.onclick = () => root.remove();

    // 4. Content Wrapper & List
    const contentWrapper = createEl('div', 'ag-panel-content-wrapper ag-default-panel-content-wrapper');
    const listViewport = createEl('div', 'ag-virtual-list-viewport ag-column-select-virtual-list-viewport');
    const listContainer = createEl('div', 'ag-virtual-list-container ag-column-select-virtual-list-container', { 'role': 'tree' });

    // 5. Populate Columns Dynamically
    columnApi.getAllColumns().forEach((col) => {
        const colId = col.getColId();
        const colDef = col.getColDef();
        const isVisible = col.isVisible();

        const item = createEl('div', 'ag-virtual-list-item ag-column-select-virtual-list-item');
        const colWrapper = createEl('div', 'ag-column-select-column');

        const cbWrapper = createEl('div', 'ag-column-select-checkbox ag-checkbox ag-input-field');
        const inputWrapper = createEl('div', 'ag-wrapper ag-checkbox-input-wrapper');
        if (isVisible) inputWrapper.classList.add('ag-checked');

        const input = createEl('input', 'ag-input-field-input ag-checkbox-input', { 'type': 'checkbox' });
        input.checked = isVisible;
        input.onchange = (e) => columnApi.setColumnVisible(colId, e.target.checked);

        const label = createEl('span', 'ag-column-select-column-label');
        label.innerText = colDef.headerName || colDef.field;

        inputWrapper.appendChild(input);
        cbWrapper.appendChild(inputWrapper);
        colWrapper.appendChild(cbWrapper);
        colWrapper.appendChild(label);
        item.appendChild(colWrapper);
        listContainer.appendChild(item);
    });

    listViewport.appendChild(listContainer);
    contentWrapper.appendChild(listViewport);
    panel.appendChild(titleBar);
    panel.appendChild(contentWrapper);
    popupWrapper.appendChild(panel);
    root.appendChild(popupWrapper);

    // 6. ATTACH TO SPECIFIC PARENT
    const targetContainer = document.querySelector('.ag-root-wrapper-body.ag-layout-normal.ag-focus-managed');

    if (targetContainer) {
        // Ensure the container is positioned relative so the absolute panel stays inside it
        targetContainer.style.position = 'relative';
        targetContainer.appendChild(root);
    } else {
        // Fallback in case the container isn't found
        document.body.appendChild(root);
    }
};
window.renderDynamicColumnPanel_old4 = function (columnApi, event) {
    const createEl = (tag, className, attributes = {}, styles = {}) => {
        const el = document.createElement(tag);
        if (className) el.className = className;
        for (let key in attributes) el.setAttribute(key, attributes[key]);
        for (let key in styles) el.style[key] = styles[key];
        return el;
    };

    // 1. Build the outer structure
    const root = createEl('div', 'ag-styled-root ag-theme-inherit-1');
    const popupWrapper = createEl('div', 'ag-popup');
    const panel = createEl('div', 'ag-panel ag-default-panel ag-dialog ag-popup-child ag-focus-managed',
        { 'role': 'dialog', 'aria-label': 'Choose Columns' },
        { 'position': 'absolute', 'top': '10px', 'left': '10px', 'width': '300px', 'height': '400px', 'z-index': '1000' }
    );

    // 2. Title Bar
    const titleBar = createEl('div', 'ag-panel-title-bar ag-default-panel-title-bar ag-unselectable');
    titleBar.innerHTML = `<span class="ag-panel-title-bar-title">Choose Columns</span>`;
    const closeBtn = createEl('div', 'ag-button ag-panel-title-bar-button');
    closeBtn.appendChild(createEl('span', 'ag-icon ag-icon-cross'));
    titleBar.appendChild(closeBtn);
    closeBtn.onclick = () => root.remove();

    // 3. Header Section (NEW)
    const header = createEl('div', 'ag-column-select-header', { 'role': 'presentation' });

    // Select All Checkbox
    const headerCbWrapper = createEl('div', 'ag-column-select-header-checkbox ag-checkbox ag-input-field');
    const headerCbInput = createEl('input', 'ag-input-field-input ag-checkbox-input', { 'type': 'checkbox' });
    headerCbInput.checked = true; // Default to all checked
    headerCbInput.onchange = (e) => {
        const isChecked = e.target.checked;
        const allColIds = columnApi.getAllColumns().map(c => c.getColId());
        columnApi.setColumnsVisible(allColIds, isChecked);
        // Sync the individual checkboxes
        document.querySelectorAll('.ag-column-select-column .ag-checkbox-input').forEach(cb => cb.checked = isChecked);
    };
    headerCbWrapper.appendChild(headerCbInput);

    // Filter Input
    const filterWrapper = createEl('div', 'ag-column-select-header-filter-wrapper ag-text-field ag-input-field');
    const filterInput = createEl('input', 'ag-input-field-input ag-text-field-input', { 'type': 'text', 'placeholder': 'Search...' });
    filterWrapper.appendChild(filterInput);

    header.appendChild(headerCbWrapper);
    header.appendChild(filterWrapper);

    // 4. Content & List
    const contentWrapper = createEl('div', 'ag-panel-content-wrapper');
    const listViewport = createEl('div', 'ag-virtual-list-viewport');
    const listContainer = createEl('div', 'ag-virtual-list-container');

    // 5. Populate List
    const allCols = columnApi.getAllColumns();
    allCols.forEach((col) => {
        const item = createEl('div', 'ag-virtual-list-item');
        const colWrapper = createEl('div', 'ag-column-select-column');
        const input = createEl('input', 'ag-checkbox-input', { 'type': 'checkbox' });
        input.checked = col.isVisible();
        input.onchange = (e) => columnApi.setColumnVisible(col.getColId(), e.target.checked);

        const label = createEl('span', 'ag-column-select-column-label');
        label.innerText = col.getColDef().headerName || col.getColId();

        colWrapper.appendChild(input);
        colWrapper.appendChild(label);
        item.appendChild(colWrapper);
        listContainer.appendChild(item);
    });

    // Filtering Logic
    filterInput.oninput = (e) => {
        const term = e.target.value.toLowerCase();
        Array.from(listContainer.children).forEach(item => {
            const label = item.querySelector('.ag-column-select-column-label').innerText.toLowerCase();
            item.style.display = label.includes(term) ? '' : 'none';
        });
    };

    // 6. Assemble
    listViewport.appendChild(listContainer);
    contentWrapper.appendChild(header); // Adding header to content wrapper
    contentWrapper.appendChild(listViewport);
    panel.appendChild(titleBar);
    panel.appendChild(contentWrapper);
    popupWrapper.appendChild(panel);
    root.appendChild(popupWrapper);

    // 7. Inject
    const target = document.querySelector('.ag-root-wrapper-body.ag-layout-normal.ag-focus-managed');
    if (target) {
        target.style.position = 'relative';
        target.appendChild(root);
    } else {
        document.body.appendChild(root);
    }
};















window.renderDynamicColumnPanel_old1234 = function (columnApi, event) {
    // 1. Helper function to create DOM nodes cleanly
    const createEl = (tag, className, attributes = {}, styles = {}) => {
        const el = document.createElement(tag);
        if (className) el.className = className;
        for (let key in attributes) el.setAttribute(key, attributes[key]);
        for (let key in styles) el.style[key] = styles[key];
        return el;
    };

    // 2. Build the outer panel structure
    const root = createEl('div', 'ag-styled-root ag-theme-inherit-1');
    const popupWrapper = createEl('div', 'ag-popup');
    const panel = createEl('div', 'ag-panel ag-default-panel ag-dialog ag-popup-child ag-focus-managed',
        { 'role': 'dialog', 'aria-label': 'Choose Columns' },
        { 'position': 'absolute', 'top': '10px', 'left': '10px', 'width': '300px', 'height': '400px', 'z-index': '1000' }
    );

    // 3. Title Bar
    const titleBar = createEl('div', 'ag-panel-title-bar ag-default-panel-title-bar ag-unselectable');
    const title = createEl('span', 'ag-panel-title-bar-title');
    title.innerText = 'Choose Columns';
    const closeBtn = createEl('div', 'ag-button ag-panel-title-bar-button');
    closeBtn.appendChild(createEl('span', 'ag-icon ag-icon-cross'));
    closeBtn.style.cursor = 'pointer';
    closeBtn.onclick = () => root.remove();
    titleBar.appendChild(title);
    titleBar.appendChild(closeBtn);

    // 4. Header Section (Search & Select All)
    const header = createEl('div', 'ag-column-select-header', { 'role': 'presentation' });

    // Select All Checkbox (Correct AG Grid hierarchy)
    const headerCbContainer = createEl('div', 'ag-column-select-header-checkbox ag-checkbox ag-input-field');
    const headerCbWrapper = createEl('div', 'ag-wrapper ag-checkbox-input-wrapper ag-checked');
    const headerCbInput = createEl('input', 'ag-input-field-input ag-checkbox-input', { 'type': 'checkbox' });
    headerCbInput.checked = true;

    headerCbInput.onchange = (e) => {
        const isChecked = e.target.checked;
        const allColIds = columnApi.getAllColumns().map(c => c.getColId());
        columnApi.setColumnsVisible(allColIds, isChecked);

        // Update visual state of all checkboxes
        document.querySelectorAll('.ag-column-select-column .ag-checkbox-input-wrapper').forEach(w => {
            isChecked ? w.classList.add('ag-checked') : w.classList.remove('ag-checked');
        });
        document.querySelectorAll('.ag-column-select-column .ag-checkbox-input').forEach(i => i.checked = isChecked);
    };

    headerCbWrapper.appendChild(headerCbInput);
    headerCbContainer.appendChild(headerCbWrapper);

    // Filter Input
    const filterWrapper = createEl('div', 'ag-column-select-header-filter-wrapper ag-text-field ag-input-field');
    const filterInput = createEl('input', 'ag-input-field-input ag-text-field-input', { 'type': 'text', 'placeholder': 'Search...' });
    filterWrapper.appendChild(filterInput);

    header.appendChild(headerCbContainer);
    header.appendChild(filterWrapper);

    // 5. List Content
    const contentWrapper = createEl('div', 'ag-panel-content-wrapper ag-default-panel-content-wrapper');
    const listViewport = createEl('div', 'ag-virtual-list-viewport ag-column-select-virtual-list-viewport');
    const listContainer = createEl('div', 'ag-virtual-list-container ag-column-select-virtual-list-container');

    // 6. Populate Rows
    columnApi.getAllColumns().forEach((col) => {
        const item = createEl('div', 'ag-virtual-list-item ag-column-select-virtual-list-item');
        const colWrapper = createEl('div', 'ag-column-select-column');

        // Correct Checkbox Hierarchy for AG Grid Theme
        const cbContainer = createEl('div', 'ag-column-select-checkbox ag-checkbox ag-input-field');
        const cbWrapper = createEl('div', 'ag-wrapper ag-checkbox-input-wrapper');
        if (col.isVisible()) cbWrapper.classList.add('ag-checked');

        const input = createEl('input', 'ag-input-field-input ag-checkbox-input', { 'type': 'checkbox' });
        input.checked = col.isVisible();

        input.onchange = (e) => {
            columnApi.setColumnVisible(col.getColId(), e.target.checked);
            e.target.checked ? cbWrapper.classList.add('ag-checked') : cbWrapper.classList.remove('ag-checked');
        };

        const label = createEl('span', 'ag-column-select-column-label');
        label.innerText = col.getColDef().headerName || col.getColId();

        // Assembly
        cbWrapper.appendChild(input);
        cbContainer.appendChild(cbWrapper);
        colWrapper.appendChild(cbContainer);
        colWrapper.appendChild(label);
        item.appendChild(colWrapper);
        listContainer.appendChild(item);
    });

    // Filtering logic
    filterInput.oninput = (e) => {
        const term = e.target.value.toLowerCase();
        Array.from(listContainer.children).forEach(item => {
            const labelText = item.querySelector('.ag-column-select-column-label').innerText.toLowerCase();
            item.style.display = labelText.includes(term) ? '' : 'none';
        });
    };

    // 7. Assemble and Inject
    listViewport.appendChild(listContainer);
    contentWrapper.appendChild(header);
    contentWrapper.appendChild(listViewport);
    panel.appendChild(titleBar);
    panel.appendChild(contentWrapper);
    popupWrapper.appendChild(panel);
    root.appendChild(popupWrapper);

    const target = document.querySelector('.ag-root-wrapper-body.ag-layout-normal.ag-focus-managed');
    if (target) {
        target.style.position = 'relative';
        target.appendChild(root);
    } else {
        document.body.appendChild(root);
    }
};

window.renderDynamicColumnPanel_alpine = function (columnApi, event) {
    console.log('clicked');
    // 1. Helper to create DOM nodes
    const createEl = (tag, className, attributes = {}, styles = {}) => {
        const el = document.createElement(tag);
        if (className) el.className = className;
        for (let key in attributes) el.setAttribute(key, attributes[key]);
        for (let key in styles) el.style[key] = styles[key];
        return el;
    };

    // 2. Root Structure matching your provided HTML
    const root = createEl('div', 'ag-theme-balham ag-popup');
    const menu = createEl('div', 'ag-tabs ag-menu ag-focus-managed ag-ltr ag-popup-child ag-keyboard-focus',
        { 'role': 'dialog', 'aria-label': 'Column Menu' },
        { 'position': 'absolute', 'left': `${event.clientX}px`, 'top': `${event.clientY}px` }
    );
    const body = createEl('div', 'ag-tabs-body ag-menu-body', { 'role': 'presentation' });
    const wrapper = createEl('div', 'ag-menu-column-select-wrapper');
    const colSelect = createEl('div', 'ag-column-select ag-focus-managed ag-menu-column-select');

    // 3. Header Section (Search & Select All)
    const header = createEl('div', 'ag-column-select-header', { 'role': 'presentation', 'tabindex': '-1' });

    // Select All Checkbox Hierarchy
    const headerCb = createEl('div', 'ag-column-select-header-checkbox ag-checkbox ag-input-field', { 'role': 'presentation' });
    const headerWrapper = createEl('div', 'ag-wrapper ag-checkbox-input-wrapper ag-checked', { 'role': 'presentation' });
    const headerInput = createEl('input', 'ag-input-field-input ag-checkbox-input', { 'type': 'checkbox' });

    headerInput.onchange = (e) => {
        const isChecked = e.target.checked;
        const allColIds = columnApi.getAllColumns().map(c => c.getColId());
        columnApi.setColumnsVisible(allColIds, isChecked);
        // Sync visuals
        document.querySelectorAll('.ag-checkbox-input-wrapper').forEach(w =>
            isChecked ? w.classList.add('ag-checked') : w.classList.remove('ag-checked')
        );
    };

    headerWrapper.appendChild(headerInput);
    headerCb.appendChild(headerWrapper);

    // Filter Input
    const filterWrapper = createEl('div', 'ag-column-select-header-filter-wrapper ag-text-field ag-input-field', { 'role': 'presentation' });
    const filterInput = createEl('input', 'ag-input-field-input ag-text-field-input', { 'type': 'text', 'placeholder': 'Search...' });
    filterWrapper.appendChild(filterInput);

    header.appendChild(headerCb);
    header.appendChild(filterWrapper);

    // 4. List Section
    const listWrapper = createEl('div', 'ag-column-select-list', { 'role': 'presentation' });
    const viewport = createEl('div', 'ag-virtual-list-viewport ag-column-select-virtual-list-viewport ag-focus-managed', { 'role': 'presentation' });
    const container = createEl('div', 'ag-virtual-list-container ag-column-select-virtual-list-container', { 'role': 'tree', 'aria-label': 'Column List' });

    // 5. Populate Rows
    columnApi.getAllColumns().forEach((col, index) => {
        const item = createEl('div', 'ag-virtual-list-item ag-column-select-virtual-list-item',
            { 'role': 'treeitem', 'aria-label': `${col.getColDef().headerName} Column` }
        );
        const colContainer = createEl('div', 'ag-column-select-column ag-column-select-indent-0', { 'aria-hidden': 'true' });

        // Checkbox Hierarchy
        const cb = createEl('div', 'ag-column-select-checkbox ag-checkbox ag-input-field', { 'role': 'presentation' });
        const cbWrapper = createEl('div', 'ag-wrapper ag-checkbox-input-wrapper', { 'role': 'presentation' });
        if (col.isVisible()) cbWrapper.classList.add('ag-checked');

        const input = createEl('input', 'ag-input-field-input ag-checkbox-input', { 'type': 'checkbox', 'tabindex': '-1' });
        input.checked = col.isVisible();
        input.onchange = (e) => {
            columnApi.setColumnVisible(col.getColId(), e.target.checked);
            e.target.checked ? cbWrapper.classList.add('ag-checked') : cbWrapper.classList.remove('ag-checked');
        };

        const label = createEl('span', 'ag-column-select-column-label');
        label.innerText = col.getColDef().headerName || col.getColId();

        // Assembly
        cbWrapper.appendChild(input);
        cb.appendChild(cbWrapper);
        colContainer.appendChild(cb);
        colContainer.appendChild(label);
        item.appendChild(colContainer);
        container.appendChild(item);
    });

    // 6. Assemble the Tree
    viewport.appendChild(container);
    listWrapper.appendChild(viewport);
    colSelect.appendChild(header);
    colSelect.appendChild(listWrapper);
    wrapper.appendChild(colSelect);
    body.appendChild(wrapper);
    menu.appendChild(body);
    root.appendChild(menu);

    // 7. Inject to grid body
    const target = document.querySelector('.ag-root-wrapper-body.ag-layout-normal.ag-focus-managed');
    target ? target.appendChild(root) : document.body.appendChild(root);
};









function updateHeaderCheckboxState(headerCbInput, headerCbWrapper) {
    // 1. Get all individual column checkboxes
    const allColCheckboxes = document.querySelectorAll('.ag-column-select-column .ag-checkbox-input');
    const checkedBoxes = Array.from(allColCheckboxes).filter(cb => cb.checked);

    // 2. Clear current header state
    headerCbWrapper.classList.remove('ag-checked', 'ag-indeterminate');
    headerCbInput.checked = false;
    headerCbInput.indeterminate = false;

    // 3. Determine state
    if (checkedBoxes.length === allColCheckboxes.length) {
        // All checked
        headerCbInput.checked = true;
        headerCbWrapper.classList.add('ag-checked');
    } else if (checkedBoxes.length > 0) {
        // Some checked (Indeterminate)
        headerCbInput.indeterminate = true;
        headerCbWrapper.classList.add('ag-indeterminate');
    } else {
        // None checked
        headerCbInput.checked = false;
    }
}
window.renderDynamicColumnPanel = function (columnApi, event) {
    // 1. Helper to create DOM nodes
    const createEl = (tag, className, attributes = {}, styles = {}) => {
        const el = document.createElement(tag);
        if (className) el.className = className;
        for (let key in attributes) el.setAttribute(key, attributes[key]);
        for (let key in styles) el.style[key] = styles[key];
        return el;
    };
    const target = document.querySelector(
        '.ag-root-wrapper-body.ag-layout-normal.ag-focus-managed'
    );

    const rect = target.getBoundingClientRect();

    const left = event.clientX - rect.left;
    const top = event.clientY - rect.top;
    // 2. Shell Structure (Balham Theme)
    const root = createEl('div', 'ag-theme-balham ag-popup');
    const menu = createEl('div', 'ag-tabs ag-menu ag-focus-managed ag-ltr ag-popup-child',
        { 'role': 'dialog', 'aria-label': 'Column Menu' },
        { 'position': 'absolute', 'left': `${left}px`, 'top': `${top }px` }
    );

    //// 3. Tabs Header
    //const header = createEl('div', 'ag-tabs-header ag-menu-header', { 'role': 'tablist' });
    //const tabs = [
    //    { label: 'general', icon: 'ag-icon-menu' },
    //    { label: 'filter', icon: 'ag-icon-filter' },
    //    { label: 'columns', icon: 'ag-icon-columns', selected: true }
    //];

    //tabs.forEach(tab => {
    //    const span = createEl('span', tab.selected ? 'ag-tab ag-tab-selected' : 'ag-tab', { 'role': 'tab', 'tabindex': '-1', 'aria-label': tab.label });
    //    span.appendChild(createEl('span', `ag-icon ${tab.icon}`, { 'unselectable': 'on', 'role': 'presentation' }));
    //    header.appendChild(span);
    //});
    ////menu.appendChild(header);
    // 3. NEW: Panel Title Bar (Requested Structure)
    const titleBar = createEl('div', 'ag-panel-title-bar ag-default-panel-title-bar ag-unselectable', { 'data-ref': 'eTitleBar' });
    const titleSpan = createEl('span', 'ag-panel-title-bar-title ag-default-panel-title-bar-title', { 'data-ref': 'eTitle' });
    titleSpan.innerText = 'Choose Columns';

    const buttonsDiv = createEl('div', 'ag-panel-title-bar-buttons ag-default-panel-title-bar-buttons', { 'data-ref': 'eTitleBarButtons' });
    const closeBtn = createEl('div', 'ag-button ag-panel-title-bar-button');
    const closeIcon = createEl('span', 'ag-icon ag-icon-cross ag-panel-title-bar-button-icon', { 'role': 'presentation', 'unselectable': 'on' });

    closeBtn.appendChild(closeIcon);
    closeBtn.onclick = () => root.remove();
    buttonsDiv.appendChild(closeBtn);
    titleBar.appendChild(titleSpan);
    titleBar.appendChild(buttonsDiv);
    menu.appendChild(titleBar);


    // 4. Body Structure
    const body = createEl('div', 'ag-tabs-body ag-menu-body', { 'role': 'presentation' });
    const wrapper = createEl('div', 'ag-menu-column-select-wrapper');
    const colSelect = createEl('div', 'ag-column-select ag-focus-managed ag-menu-column-select');

    // 5. Column Select Header (Select All + Search)
    const colHeader = createEl('div', 'ag-column-select-header', { 'role': 'presentation', 'tabindex': '-1' });

    // Select All Checkbox
    const cbHeader = createEl('div', 'ag-column-select-header-checkbox ag-checkbox ag-input-field', { 'role': 'presentation' });
    const cbWrapper = createEl('div', 'ag-wrapper ag-input-wrapper ag-checkbox-input-wrapper ag-checked', { 'role': 'presentation' });
    const cbInput = createEl('input', 'ag-input-field-input ag-checkbox-input', { 'type': 'checkbox' });
    cbInput.checked = true;
    //cbInput.onchange = (e) => {
    //    const isChecked = e.target.checked;
    //    const allColIds = columnApi.getAllColumns().map(c => c.getColId());
    //    columnApi.setColumnsVisible(allColIds, isChecked);
    //    // Visual sync
    //    document.querySelectorAll('.ag-checkbox-input-wrapper').forEach(w =>
    //        isChecked ? w.classList.add('ag-checked') : w.classList.remove('ag-checked')
    //    );
    //};
    // --- RECTIFIED ONCHANGE HANDLER ---
    cbInput.onchange = (e) => {
        const isChecked = e.target.checked;

        cbInput.indeterminate = false;
        cbWrapper.classList.remove('ag-indeterminate');

        const allColIds = columnApi.getAllColumns().map(c => c.getColId());
        columnApi.setColumnsVisible(allColIds, isChecked);

        const colItems = container.querySelectorAll('.ag-column-select-column');
        colItems.forEach(colDiv => {
            const input = colDiv.querySelector('.ag-checkbox-input');
            const wrapper = colDiv.querySelector('.ag-checkbox-input-wrapper');

            if (input) input.checked = isChecked;
            if (wrapper) {
                wrapper.classList.remove('ag-indeterminate');
                if (isChecked) {
                    wrapper.classList.add('ag-checked');
                } else {
                    wrapper.classList.remove('ag-checked');
                }
            }
        });

        updateHeaderCheckboxState(cbInput, cbWrapper);
    };
    cbWrapper.appendChild(cbInput);
    cbHeader.appendChild(cbWrapper);

    // Filter
    const filter = createEl('div', 'ag-column-select-header-filter-wrapper ag-text-field ag-input-field', { 'role': 'presentation' });
    const filterInput = createEl('input', 'ag-input-field-input ag-text-field-input', { 'type': 'text', 'placeholder': 'Search...' });
    filter.appendChild(filterInput);

    colHeader.appendChild(cbHeader);
    colHeader.appendChild(filter);
    colSelect.appendChild(colHeader);

    // 6. List
    const listWrapper = createEl('div', 'ag-column-select-list', { 'role': 'presentation' });
    const viewport = createEl('div', 'ag-virtual-list-viewport ag-column-select-virtual-list-viewport ag-focus-managed', { 'role': 'presentation' });
    const container = createEl('div', 'ag-virtual-list-container ag-column-select-virtual-list-container', { 'role': 'tree', 'aria-label': 'Column List', 'style': 'height: 190px;overflow-y:auto;' });

    // 7. Loop Columns
    columnApi.getAllColumns().forEach((col) => {
        const item = createEl('div', 'ag-virtual-list-item ag-column-select-virtual-list-item', { 'role': 'treeitem' });
        const colDiv = createEl('div', 'ag-column-select-column ag-column-select-indent-0', { 'aria-hidden': 'true' });

        const cb = createEl('div', 'ag-column-select-checkbox ag-checkbox ag-input-field', { 'role': 'presentation' });
        const wrapper = createEl('div', 'ag-wrapper ag-input-wrapper ag-checkbox-input-wrapper', { 'role': 'presentation' });
        if (col.isVisible()) wrapper.classList.add('ag-checked');

        const input = createEl('input', 'ag-input-field-input ag-checkbox-input', { 'type': 'checkbox' });
        input.checked = col.isVisible();
        input.onchange = (e) => {
            columnApi.setColumnVisible(col.getColId(), e.target.checked);
            e.target.checked ? wrapper.classList.add('ag-checked') : wrapper.classList.remove('ag-checked');

            // here
            updateHeaderCheckboxState(cbInput, cbWrapper);
        };

        const label = createEl('span', 'ag-column-select-column-label');
        label.innerText = col.getColDef().headerName || col.getColId();
        label.onclick = () => {
            // This toggles the checkbox and triggers the 'onchange' you already defined!
            input.click();
        };

        wrapper.appendChild(input);
        cb.appendChild(wrapper);
        colDiv.appendChild(cb);
        colDiv.appendChild(label);
        item.appendChild(colDiv);
        container.appendChild(item);
    });
    

    // Filtering logic
    filterInput.oninput = (e) => {
        const term = e.target.value.toLowerCase();
        Array.from(container.children).forEach(item => {
            const labelText = item.querySelector('.ag-column-select-column-label').innerText.toLowerCase();
            item.style.display = labelText.includes(term) ? '' : 'none';
        });
    };

    // Close logic (Clicking outside removes it)
    requestAnimationFrame(() => {
        document.addEventListener('click', function closeMenu(e) {
            if (!menu.contains(e.target)) {
                root.remove();
                document.removeEventListener('click', closeMenu);
            }
        }, { once: true });
        const closeMenu = (e) => {
            // Check if the click is OUTSIDE the menu
            if (!menu.contains(e.target)) {
                root.remove(); // Remove the element
                document.removeEventListener('click', closeMenu); // Clean up the listener
            }
        };
        // 2. Add a tiny delay (setTimeout) before attaching the listener.
        // This prevents the click that *opened* the menu from instantly closing it.
        setTimeout(() => {
            document.addEventListener('click', closeMenu);
        }, 100);

        // 3. IMPORTANT: If the user presses "Escape" to close the menu, 
        // make sure we clean up the listener to prevent memory leaks.
        closeBtn.onclick = () => {
            root.remove();
            document.removeEventListener('click', closeMenu);
        };
    });
    // 8. Assemble
    viewport.appendChild(container);
    listWrapper.appendChild(viewport);
    colSelect.appendChild(listWrapper);
    wrapper.appendChild(colSelect);
    body.appendChild(wrapper);
    menu.appendChild(body);
    root.appendChild(menu);

    //const target = document.querySelector('.ag-root-wrapper-body.ag-layout-normal.ag-focus-managed');
    /*target ? target.appendChild(root) : document.body.appendChild(root);*/
    target ? target.appendChild(root) : document.body.appendChild(root);
    updateHeaderCheckboxState(cbInput, cbWrapper);
    //window.tempabc = root;
    //console.log('succs', root);
};






















window.renderDynamicColumnPanel2 = function (columnApi, event) {
    // 1. CLEANUP: Remove any existing popup before creating a new one
    const existingMenu = document.getElementById('custom-column-menu');
    if (existingMenu) existingMenu.remove();

    const createEl = (tag, className, attributes = {}, styles = {}) => {
        const el = document.createElement(tag);
        if (className) el.className = className;
        for (let key in attributes) el.setAttribute(key, attributes[key]);
        for (let key in styles) el.style[key] = styles[key];
        return el;
    };

    // 2. Shell Structure (Balham Theme)
    // ID added for cleanup, position: fixed to escape grid boundaries
    const root = createEl('div', 'ag-theme-balham ag-popup', { 'id': 'custom-column-menu' });

    const menu = createEl('div', 'ag-tabs ag-menu ag-focus-managed ag-ltr ag-popup-child',
        { 'role': 'dialog', 'aria-label': 'Column Menu' },
        {
            'position': 'fixed', // CHANGED: Fixed positioning ignores grid overflow
            'left': `${event.clientX}px`,
            'top': `${event.clientY}px`,
            'z-index': '9999'   // Ensures it stays on top of everything
        }
    );

    // 3. NEW: Panel Title Bar (Requested Structure)
    const titleBar = createEl('div', 'ag-panel-title-bar ag-default-panel-title-bar ag-unselectable', { 'data-ref': 'eTitleBar' });
    const titleSpan = createEl('span', 'ag-panel-title-bar-title ag-default-panel-title-bar-title', { 'data-ref': 'eTitle' });
    titleSpan.innerText = 'Choose Columns';

    const buttonsDiv = createEl('div', 'ag-panel-title-bar-buttons ag-default-panel-title-bar-buttons', { 'data-ref': 'eTitleBarButtons' });
    const closeBtn = createEl('div', 'ag-button ag-panel-title-bar-button');
    const closeIcon = createEl('span', 'ag-icon ag-icon-cross ag-panel-title-bar-button-icon', { 'role': 'presentation', 'unselectable': 'on' });

    closeBtn.appendChild(closeIcon);
    closeBtn.onclick = () => root.remove();
    buttonsDiv.appendChild(closeBtn);
    titleBar.appendChild(titleSpan);
    titleBar.appendChild(buttonsDiv);
    menu.appendChild(titleBar);

    // 4. Body Structure
    const body = createEl('div', 'ag-tabs-body ag-menu-body', { 'role': 'presentation' });
    const wrapper = createEl('div', 'ag-menu-column-select-wrapper');
    const colSelect = createEl('div', 'ag-column-select ag-focus-managed ag-menu-column-select');

    // 5. Column Select Header
    const colHeader = createEl('div', 'ag-column-select-header', { 'role': 'presentation', 'tabindex': '-1' });

    // Select All Checkbox
    const cbHeader = createEl('div', 'ag-column-select-header-checkbox ag-checkbox ag-input-field', { 'role': 'presentation' });
    const cbWrapper = createEl('div', 'ag-wrapper ag-input-wrapper ag-checkbox-input-wrapper ag-checked', { 'role': 'presentation' });
    const cbInput = createEl('input', 'ag-input-field-input ag-checkbox-input', { 'type': 'checkbox' });
    cbInput.checked = true;
    cbInput.onchange = (e) => {
        const isChecked = e.target.checked;
        const allColIds = columnApi.getAllColumns().map(c => c.getColId());
        columnApi.setColumnsVisible(allColIds, isChecked);
        document.querySelectorAll('.ag-checkbox-input-wrapper').forEach(w =>
            isChecked ? w.classList.add('ag-checked') : w.classList.remove('ag-checked')
        );
    };
    cbWrapper.appendChild(cbInput);
    cbHeader.appendChild(cbWrapper);

    // Filter
    const filter = createEl('div', 'ag-column-select-header-filter-wrapper ag-text-field ag-input-field', { 'role': 'presentation' });
    const filterInput = createEl('input', 'ag-input-field-input ag-text-field-input', { 'type': 'text', 'placeholder': 'Search...' });
    filter.appendChild(filterInput);

    colHeader.appendChild(cbHeader);
    colHeader.appendChild(filter);
    colSelect.appendChild(colHeader);

    // 6. List
    const listWrapper = createEl('div', 'ag-column-select-list', { 'role': 'presentation' });
    const viewport = createEl('div', 'ag-virtual-list-viewport ag-column-select-virtual-list-viewport ag-focus-managed', { 'role': 'presentation' });
    const container = createEl('div', 'ag-virtual-list-container ag-column-select-virtual-list-container', { 'role': 'tree', 'aria-label': 'Column List', 'style': 'height: 240px;' });

    // 7. Loop Columns
    columnApi.getAllColumns().forEach((col) => {
        const item = createEl('div', 'ag-virtual-list-item ag-column-select-virtual-list-item', { 'role': 'treeitem' });
        const colDiv = createEl('div', 'ag-column-select-column ag-column-select-indent-0', { 'aria-hidden': 'true' });

        const cb = createEl('div', 'ag-column-select-checkbox ag-checkbox ag-input-field', { 'role': 'presentation' });
        const wrapper = createEl('div', 'ag-wrapper ag-input-wrapper ag-checkbox-input-wrapper', { 'role': 'presentation' });
        if (col.isVisible()) wrapper.classList.add('ag-checked');

        const input = createEl('input', 'ag-input-field-input ag-checkbox-input', { 'type': 'checkbox' });
        input.checked = col.isVisible();
        input.onchange = (e) => {
            columnApi.setColumnVisible(col.getColId(), e.target.checked);
            e.target.checked ? wrapper.classList.add('ag-checked') : wrapper.classList.remove('ag-checked');
        };

        const label = createEl('span', 'ag-column-select-column-label');
        label.innerText = col.getColDef().headerName || col.getColId();

        wrapper.appendChild(input);
        cb.appendChild(wrapper);
        colDiv.appendChild(cb);
        colDiv.appendChild(label);
        item.appendChild(colDiv);
        container.appendChild(item);
    });

    filterInput.oninput = (e) => {
        const term = e.target.value.toLowerCase();
        Array.from(container.children).forEach(item => {
            const labelText = item.querySelector('.ag-column-select-column-label').innerText.toLowerCase();
            item.style.display = labelText.includes(term) ? '' : 'none';
        });
    };

    // Close logic
    document.addEventListener('click', function closeMenu(e) {
        if (!menu.contains(e.target) && e.target !== event.target) {
            root.remove();
            document.removeEventListener('click', closeMenu);
        }
    });

    // 8. Assemble and Inject
    viewport.appendChild(container);
    listWrapper.appendChild(viewport);
    colSelect.appendChild(listWrapper);
    wrapper.appendChild(colSelect);
    body.appendChild(wrapper);
    menu.appendChild(body);
    root.appendChild(menu);
    const target = document.querySelector('.ag-root-wrapper-body.ag-layout-normal.ag-focus-managed');
    target ? target.appendChild(root) : document.body.appendChild(root);
    // CHANGED: Inject into body, not the grid container
    //document.body.appendChild(root);
};
// Global Date Formatter Function
// 1. GLOBAL DATE FORMATTER USING C# DATE PATTERN
window.currentDateFormat = "dd/MM/yyyy"; // Fallback default

window.shortDateFormatter = function (params) {
    if (!params.value) return '';
    const date = new Date(params.value);

    // Return original string if it's not a valid date object
    if (isNaN(date.getTime())) return params.value;

    const year = date.getFullYear();
    const month = String(date.getMonth() + 1).padStart(2, '0');
    const day = String(date.getDate()).padStart(2, '0');

    let fmt = window.currentDateFormat || "dd/MM/yyyy";

    // Replaces C# short date tokens with actual date components
    return fmt
        .replace("yyyy", year)
        .replace("yy", String(year).slice(-2))
        .replace("MM", month)
        .replace("dd", day);
};

// Helper function to auto-assign formatters and filters
function processColumnDefs(defs) {
    if (!defs || !Array.isArray(defs)) return defs;

    return defs.map(col => {
        
        // Auto-attach shortDateFormatter to any column field containing 'date' or 'dt'
        if (col.filter && col.filter === 'agDateColumnFilter') {
            /*console.log('co:', col.filter);*/
            //col.valueFormatter = window.shortDateFormatter;
            col.valueFormatter = window.formatDate;
            //col.filterParams = { browserDatePicker: true };
            col.filterParams = {
                browserDatePicker: true,

                // --- ADD THIS COMPARATOR TO FIX IN-RANGE CLIENT-SIDE FILTERING ---
                comparator: function (filterLocalDateAtMidnight, cellValue) {
                    if (!cellValue) return -1;

                    // Extract YYYY-MM-DD from ISO string ("2026-07-20T17:39:43.497")
                    const dateParts = cellValue.split('T')[0].split('-');
                    if (dateParts.length < 3) return -1;

                    const year = Number(dateParts[0]);
                    const month = Number(dateParts[1]) - 1; // JS Months are 0-indexed (0 = Jan)
                    const day = Number(dateParts[2]);

                    // Create midnight JS Date object for accurate comparison
                    const cellDateAtMidnight = new Date(year, month, day);

                    if (cellDateAtMidnight.getTime() === filterLocalDateAtMidnight.getTime()) {
                        return 0;
                    }
                    if (cellDateAtMidnight < filterLocalDateAtMidnight) {
                        return -1;
                    }
                    if (cellDateAtMidnight > filterLocalDateAtMidnight) {
                        return 1;
                    }
                    return 0;
                }
            };
        }
        return col;
    });
}




//window.setsearchtext = function (a) {
//    window.currentGridSearchTerm = a;
//}








//// Add this to your JS file (e.g., site.js or a script block)
//class HighlightCellRenderer {
//    init(params) {
//        this.eGui = document.createElement('span');

//        // 1. Get the raw value of the cell
//        let value = params.value != null ? String(params.value) : '';
//        if (!value) return;

//        // 2. Get our custom search term (set by your Blazor code)
//        let searchTerm = (window.currentGridSearchTerm || "").trim();

//        // 3. Apply highlighting only if there is a search term
//        if (searchTerm !== '') {
//            try {
//                // Escape special regex characters
//                let escapedTerm = searchTerm.replace(/[\-\[\]\/\{\}\(\)\*\+\?\.\\\^\$\|]/g, "\\$&");
//                let regex = new RegExp('(' + escapedTerm + ')', 'ig');

//                // Wrap in yellow span
//                this.eGui.innerHTML = value.replace(regex, '<span style="background-color:yellow;color:black;">$&</span>');
//            } catch (e) {
//                // If regex fails (e.g. invalid user input), fallback to plain text
//                this.eGui.innerText = value;
//            }
//        } else {
//            // No search, just text
//            this.eGui.innerText = value;
//        }
//    }

//    getGui() {
//        return this.eGui;
//    }
//}

//// Ensure it is globally available to the grid
//window.HighlightCellRenderer = HighlightCellRenderer;