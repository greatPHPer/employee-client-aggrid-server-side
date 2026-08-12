//window.agGridInterop = window.agGridInterop || {};

//window.agGridInterop.createGridWithSelectionCallback = function (element, columnDefs, rowData, dotNetRef) {
//    if (!element) {
//        console.error('agGridInterop.createGridWithSelectionCallback: element is null');
//        return;
//    }

//    const gridOptions = {
//        columnDefs: columnDefs || [],
//        rowData: rowData || [],
//        defaultColDef: { sortable: true, filter: true, resizable: true },
//        rowSelection: 'single',
//        onSelectionChanged: function (event) {
//            const rows = event.api.getSelectedRows();
//            dotNetRef.invokeMethodAsync('OnSelectionChanged', rows);
//        }
//    };
//    //const eGridDiv = document.getElementsByClassName('ag-theme-alpine');
//    //if (!eGridDiv) {
//    //    console.error('createGridWithSelectionCallback: element not found for id', elementId);
//    //    return;
//    //}
//    // element is the actual DOM node when called from Blazor with ElementReference
//    //new agGrid.Grid(element, gridOptions);

//    // optional: expose api for later use
//    window._myGridApi = gridOptions.api || null;
//};
