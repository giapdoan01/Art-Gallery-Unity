mergeInto(LibraryManager.library, {
    
    JS_FileUploader_OpenDialog: function(objectNamePtr, callbackNamePtr, acceptTypesPtr) {
        var objectName = UTF8ToString(objectNamePtr);
        var callbackName = UTF8ToString(callbackNamePtr);
        var acceptTypes = UTF8ToString(acceptTypesPtr);
        
        console.log('[FileUploader] Opening file dialog...');
        
        var fileInput = document.createElement('input');
        fileInput.type = 'file';
        fileInput.accept = acceptTypes;
        fileInput.style.display = 'none';
        document.body.appendChild(fileInput);
        
        fileInput.onchange = function(event) {
            var file = event.target.files[0];
            
            if (!file) {
                console.log('[FileUploader] No file selected');
                document.body.removeChild(fileInput);
                return;
            }
            
            console.log('[FileUploader] File selected:', file.name, file.size, 'bytes');
            
            var reader = new FileReader();
            
            reader.onload = function(e) {
                var base64 = e.target.result;
                
                console.log('[FileUploader] File loaded, sending to Unity...');
                
                SendMessage(objectName, callbackName, JSON.stringify({
                    fileName: file.name,
                    fileSize: file.size,
                    fileType: file.type,
                    base64Data: base64
                }));
                
                console.log('[FileUploader] Data sent successfully');
            };
            
            reader.onerror = function(error) {
                console.error('[FileUploader] Error:', error);
                SendMessage(objectName, callbackName, JSON.stringify({
                    error: 'Failed to read file'
                }));
            };
            
            reader.readAsDataURL(file);
            document.body.removeChild(fileInput);
        };
        
        fileInput.click();
    }
    
});
