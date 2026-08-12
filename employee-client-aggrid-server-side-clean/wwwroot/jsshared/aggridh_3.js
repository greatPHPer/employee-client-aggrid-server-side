window.agGridInterop = window.agGridInterop || {};
window.currentGridSearchTerm = "";
window.currentGridSearchFilters = [];
window.isPaginationChanging = false;

// 1. UPDATE SEARCH HIGHLIGHT FILTERS
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

// 2. CELL HIGHLIGHTER RENDERER
if (!window.HighlightCellRenderer) {
    window.HighlightCellRenderer = class {
        init(params) {
            this.eGui = document.createElement('span');
            let value = params.value != null ? String(params.value) : '';
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

// 3. HEADER CHECKBOX STATE CALCULATOR
function updateHeaderCheckboxState(headerCbInput, headerCbWrapper) {
    const allColCheckboxes = document.querySelectorAll('.ag-column-select-column .ag-checkbox-input');
    const checkedBoxes = Array.from(allColCheckboxes).filter(cb => cb.checked);

    headerCbWrapper.classList.remove('ag-checked', 'ag-indeterminate');
    headerCbInput.checked = false;
    headerCbInput.indeterminate = false;

    if (checkedBoxes.length === allColCheckboxes.length && allColCheckboxes.length > 0) {
        headerCbInput.checked = true;
        headerCbWrapper.classList.add('ag-checked');
    } else if (checkedBoxes.length > 0) {
        headerCbInput.indeterminate = true;
        headerCbWrapper.classList.add('ag-indeterminate');
    }
}

// 4. DYNAMIC COLUMN CHOOSER PANEL
window.renderDynamicColumnPanel = function (columnApi, event) {
    const existingMenu = document.getElementById('custom-column-menu');
    if (existingMenu) existingMenu.remove();

    const createEl = (tag, className, attributes = {}, styles = {}) => {
        const el = document.createElement(tag);
        if (className) el.className = className;
        for (let key in attributes) el.setAttribute(key, attributes[key]);
        for (let key in styles) el.style[key] = styles[key];
        return el;
    };

    const target = document.querySelector('.ag-root-wrapper-body.ag-layout-normal.ag-focus-managed') || document.body;
    const rect = target.getBoundingClientRect();
    const left = event.clientX - rect.left;
    const top = event.clientY - rect.top;

    const root = createEl('div', 'ag-theme-balham ag-popup', { 'id': 'custom-column-menu' });
    const menu = createEl('div', 'ag-tabs ag-menu ag-focus-managed ag-ltr ag-popup-child',
        { 'role': 'dialog', 'aria-label': 'Column Menu' },
        { 'position': 'absolute', 'left': `${left}px`, 'top': `${top}px`, 'z-index': '9999' }
    );

    // Title Bar
    const titleBar = createEl('div', 'ag-panel-title-bar ag-default-panel-title-bar ag-unselectable', { 'data-ref': 'eTitleBar' });
    const titleSpan = createEl('span', 'ag-panel-title-bar-title ag-default-panel-title-bar-title', { 'data-ref': 'eTitle' });
    titleSpan.innerText = 'Choose Columns';

    const buttonsDiv = createEl('div', 'ag-panel-title-bar-buttons ag-default-panel-title-bar-buttons', { 'data-ref': 'eTitleBarButtons' });
    const closeBtn = createEl('div', 'ag-button ag-panel-title-bar-button');
    const closeIcon = createEl('span', 'ag-icon ag-icon-cross ag-panel-title-bar-button-icon', { 'role': 'presentation', 'unselectable': 'on' });

    closeBtn.appendChild(closeIcon);
    buttonsDiv.appendChild(closeBtn);
    titleBar.appendChild(titleSpan);
    titleBar.appendChild(buttonsDiv);
    menu.appendChild(titleBar);

    // Body Structure
    const body = createEl('div', 'ag-tabs-body ag-menu-body', { 'role': 'presentation' });
    const wrapper = createEl('div', 'ag-menu-column-select-wrapper');
    const colSelect = createEl('div', 'ag-column-select ag-focus-managed ag-menu-column-select');

    // Header Checkbox
    const colHeader = createEl('div', 'ag-column-select-header', { 'role': 'presentation', 'tabindex': '-1' });
    const cbHeader = createEl('div', 'ag-column-select-header-checkbox ag-checkbox ag-input-field', { 'role': 'presentation' });
    const cbWrapper = createEl('div', 'ag-wrapper ag-input-wrapper ag-checkbox-input-wrapper ag-checked', { 'role': 'presentation' });
    const cbInput = createEl('input', 'ag-input-field-input ag-checkbox-input', { 'type': 'checkbox' });

    cbInput.checked = true;

    cbInput.onchange = (e) => {
        const isChecked = e.target.checked;
        cbInput.indeterminate = false;
        cbWrapper.classList.remove('ag-indeterminate');

        const allColIds = columnApi.getAllColumns().map(c => c.getColId());
        columnApi.setColumnsVisible(allColIds, isChecked);

        const colItems = container.querySelectorAll('.ag-column-select-column');
        colItems.forEach(colDiv => {
            const input = colDiv.querySelector('.ag-checkbox-input');
            const wrap = colDiv.querySelector('.ag-checkbox-input-wrapper');
            if (input) input.checked = isChecked;
            if (wrap) {
                wrap.classList.remove('ag-indeterminate');
                if (isChecked) wrap.classList.add('ag-checked');
                else wrap.classList.remove('ag-checked');
            }
        });

        updateHeaderCheckboxState(cbInput, cbWrapper);
    };

    cbWrapper.appendChild(cbInput);
    cbHeader.appendChild(cbWrapper);

    // Filter Search Box
    const filter = createEl('div', 'ag-column-select-header-filter-wrapper ag-text-field ag-input-field', { 'role': 'presentation' });
    const filterInput = createEl('input', 'ag-input-field-input ag-text-field-input', { 'type': 'text', 'placeholder': 'Search...' });
    filter.appendChild(filterInput);

    colHeader.appendChild(cbHeader);
    colHeader.appendChild(filter);
    colSelect.appendChild(colHeader);

    // Columns List
    const listWrapper = createEl('div', 'ag-column-select-list', { 'role': 'presentation' });
    const viewport = createEl('div', 'ag-virtual-list-viewport ag-column-select-virtual-list-viewport ag-focus-managed', { 'role': 'presentation' });
    const container = createEl('div', 'ag-virtual-list-container ag-column-select-virtual-list-container', { 'role': 'tree', 'aria-label': 'Column List', 'style': 'height: 240px;' });

    columnApi.getAllColumns().forEach((col) => {
        const item = createEl('div', 'ag-virtual-list-item ag-column-select-virtual-list-item', { 'role': 'treeitem' });
        const colDiv = createEl('div', 'ag-column-select-column ag-column-select-indent-0', { 'aria-hidden': 'true' });

        const cb = createEl('div', 'ag-column-select-checkbox ag-checkbox ag-input-field', { 'role': 'presentation' });
        const itemWrapper = createEl('div', 'ag-wrapper ag-input-wrapper ag-checkbox-input-wrapper', { 'role': 'presentation' });
        if (col.isVisible()) itemWrapper.classList.add('ag-checked');

        const input = createEl('input', 'ag-input-field-input ag-checkbox-input', { 'type': 'checkbox' });
        input.checked = col.isVisible();

        input.onchange = (e) => {
            columnApi.setColumnVisible(col.getColId(), e.target.checked);
            e.target.checked ? itemWrapper.classList.add('ag-checked') : itemWrapper.classList.remove('ag-checked');
            updateHeaderCheckboxState(cbInput, cbWrapper);
        };

        const label = createEl('span', 'ag-column-select-column-label');
        label.innerText = col.getColDef().headerName || col.getColId();
        label.style.cursor = 'pointer';
        label.onclick = () => { input.click(); };

        itemWrapper.appendChild(input);
        cb.appendChild(itemWrapper);
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

    // Outside Click & Close Logic
    const closeMenu = (e) => {
        if (!menu.contains(e.target)) {
            root.remove();
            document.removeEventListener('click', closeMenu);
        }
    };

    setTimeout(() => {
        document.addEventListener('click', closeMenu);
    }, 100);

    closeBtn.onclick = () => {
        root.remove();
        document.removeEventListener('click', closeMenu);
    };

    viewport.appendChild(container);
    listWrapper.appendChild(viewport);
    colSelect.appendChild(listWrapper);
    wrapper.appendChild(colSelect);
    body.appendChild(wrapper);
    menu.appendChild(body);
    root.appendChild(menu);

    target.appendChild(root);
    updateHeaderCheckboxState(cbInput, cbWrapper);
};

// 5. HEADER ICON INJECTION
window.addCustomHeaderMenuIcon = function (gridOptions) {
    const containers = document.querySelectorAll('.ag-cell-label-container');

    containers.forEach(container => {
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
            renderDynamicColumnPanel(gridOptions.columnApi || window._myGridApi, event);
        });

        customSpan.appendChild(innerSpan);
        container.prepend(customSpan);
    });
};

// 6. MAIN GRID CREATION / REUSE
window.agGridInterop.createOrReuseGrid = function (element, columnDefs, rowData, dotNetRef) {
    if (!element) return;

    if (columnDefs) {
        if (!columnDefs.defaultColDef) {
            columnDefs.defaultColDef = { sortable: true, filter: 'agTextColumnFilter', resizable: true };
        }
    }

    if (window._myGridApi) {
        if (columnDefs) window._myGridApi.setColumnDefs(columnDefs);
        if (rowData) window._myGridApi.setRowData(rowData);
        return;
    }

    function sizeToFit(api) {
        const gridDiv = document.querySelector('#myGridId');
        if (!gridDiv) return;
        const offsetTop = gridDiv.offsetTop;
        var newHeight = window.innerHeight - offsetTop - 20;
        newHeight = Math.max(newHeight, 300);
        gridDiv.style.height = `${newHeight}px`;
        api.sizeColumnsToFit();
    }

    const gridOptions = {
        enableCharts: true,
        enableRangeSelection: true,
        onFirstDataRendered: function (params) {
            sizeToFit(params.api);
            window.addEventListener('resize', () => sizeToFit(params.api));
            window.addCustomHeaderMenuIcon(gridOptions);
        },
        immutableData: true,
        animateRows: true,
        getRowNodeId: function (params) { return params.data.complaint_id; },
        getRowId: function (params) { return params.data.complaint_id; },
        columnDefs: columnDefs || [],
        rowData: rowData || [],
        defaultColDef: { sortable: true, filter: 'agTextColumnFilter', resizable: true },
        rowSelection: 'single',
        suppressRowClickSelection: true,
        components: {
            HighlightCellRenderer: HighlightCellRenderer
        },
        onRowClicked: function (params) {
            params.node.setSelected(!params.node.isSelected(), true);
        },
        onSelectionChanged: function (event) {
            const rows = event.api.getSelectedRows();
            if (dotNetRef) dotNetRef.invokeMethodAsync('OnSelectionChanged', rows).catch(console.error);
        },
        onSortChanged: function (event) {
            const columnState = event.columnApi ? event.columnApi.getColumnState() : event.api.getColumnState();
            const activeSorts = columnState
                .filter(s => s.sort != null)
                .map(s => `${s.colId} ${s.sort}`);

            if (dotNetRef) {
                dotNetRef.invokeMethodAsync('LoadGridDataAsync', activeSorts, null).catch(console.error);
            }
        },
        onFilterChanged: function (event) {
            const columnState = event.columnApi ? event.columnApi.getColumnState() : event.api.getColumnState();
            const activeSorts = columnState
                .filter(s => s.sort != null)
                .map(s => `${s.colId} ${s.sort}`);

            const filterModel = event.api.getFilterModel();
            const activeFilters = [];

            const mapOperator = (agType) => {
                switch (agType) {
                    case 'equals': return 'Equals';
                    case 'notEqual': return 'Not Equals';
                    case 'notContains': return 'Not Contains';
                    case 'startsWith': return 'Starts With';
                    case 'endsWith': return 'Ends With';
                    case 'contains':
                    default: return 'Contains';
                }
            };

            for (const colId in filterModel) {
                if (filterModel.hasOwnProperty(colId)) {
                    const f = filterModel[colId];
                    if (f.operator && f.condition1 && f.condition2) {
                        activeFilters.push({
                            SelectedColumn: colId,
                            SearchValue: String(f.condition1.filter),
                            FilterOperator: mapOperator(f.condition1.type),
                            NextLogicalOperator: f.operator.toUpperCase()
                        });
                        activeFilters.push({
                            SelectedColumn: colId,
                            SearchValue: String(f.condition2.filter),
                            FilterOperator: mapOperator(f.condition2.type),
                            NextLogicalOperator: "AND"
                        });
                    } else if (f.filter !== undefined) {
                        activeFilters.push({
                            SelectedColumn: colId,
                            SearchValue: String(f.filter),
                            FilterOperator: mapOperator(f.type),
                            NextLogicalOperator: "AND"
                        });
                    }
                }
            }

            if (dotNetRef) {
                dotNetRef.invokeMethodAsync('LoadGridDataAsync', activeSorts, JSON.stringify(activeFilters)).catch(console.error);
            }
        },
        pagination: true,
        paginationPageSize: 10,
        suppressPaginationPanel: false,
        onPaginationChanged: function (params) {
            if (window.isPaginationChanging) return;
            const api = params.api;
            const currentPage = api.paginationGetCurrentPage() + 1;

            if (dotNetRef) {
                window.isPaginationChanging = true;
                dotNetRef.invokeMethodAsync('OnPageChanged', currentPage)
                    .then(() => { window.isPaginationChanging = false; })
                    .catch(() => { window.isPaginationChanging = false; });
            }
        },
        onColumnVisible: () => window.addCustomHeaderMenuIcon(gridOptions),
        onColumnResized: () => window.addCustomHeaderMenuIcon(gridOptions),
        onGridColumnsChanged: () => window.addCustomHeaderMenuIcon(gridOptions),
        onBodyScroll: () => window.addCustomHeaderMenuIcon(gridOptions)
    };

    new agGrid.Grid(element, gridOptions);
    window._myGridApi = gridOptions.api || null;
};

// 7. HELPER INTEROP METHODS
window.agGridInterop.setRowData = function (rowData) {
    if (!window._myGridApi) return;
    if (typeof window._myGridApi.setRowData === 'function') {
        window._myGridApi.setRowData(rowData);
    } else if (typeof window._myGridApi.setGridOption === 'function') {
        window._myGridApi.setGridOption('rowData', rowData);
    }
};

window.agGridInterop.destroyGrid = function () {
    if (window._myGridApi) {
        try { window._myGridApi.destroy(); } catch (e) { console.warn(e); }
        window._myGridApi = null;
    }
};