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
if (!window.HighlightCellRenderer) {
    window.HighlightCellRenderer = class {
        init(params) {
            this.eGui = document.createElement('span');
            let value = params.value != null ? String(params.value) : '';
            if (!value) return;

            // Find the active search terms target matching THIS current cell's column ID
            let currentColId = params.column.getColId();

            let matchingFilters = (window.currentGridSearchFilters || [])
                .filter(f => f.col && f.col.toLowerCase() === currentColId.toLowerCase() && f.val);

            if (matchingFilters.length > 0) {
                try {
                    // Collect and escape search text targeted only to this column
                    let terms = matchingFilters.map(f => f.val);
                    let escapedTerms = terms.map(t => t.replace(/[.*+?^${}()|[\\]\\\\]/g, '\\$&'));

                    let regexPattern = '(' + escapedTerms.join('|') + ')';
                    let regex = new RegExp(regexPattern, 'ig');

                    // Highlight matches strictly within this column
                    this.eGui.innerHTML = value.replace(regex, '<span style="background-color:yellow;color:black;">$&</span>');
                } catch (e) {
                    this.eGui.innerText = value;
                }
            } else {
                // No matching filter for this specific column, display normal text
                this.eGui.innerText = value;
            }
        }
        getGui() { return this.eGui; }
    }
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
// create once, reuse later
window.agGridInterop.createOrReuseGrid = function (element, columnDefs, rowData, dotNetRef) {
    if (!element) {
        console.error('createOrReuseGrid: element is null');
        return;
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
    function onFirstDataRendered(params) {
        sizeToFit(params.api);
        window.addEventListener('resize', () => sizeToFit(params.api));
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
        pagination: false,








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
        }
    };

    new agGrid.Grid(element, gridOptions);

    console.log("Grid API:", gridOptions.api);
    console.log("Element connected:", element.isConnected);
    // gridOptions.api is populated after creation
    window._myGridApi = gridOptions.api || null;
};

// update row data later
window.agGridInterop.setRowData = function (rowData) {
    if (!window._myGridApi) {
        console.warn('setRowData: gridApi not available');
        return;
    }
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