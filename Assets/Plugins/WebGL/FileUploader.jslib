mergeInto(LibraryManager.library, {
    OpenFilePicker: function() {
        console.log('[jslib] OpenFilePicker called');
        window.OpenFilePicker();
    }
});
