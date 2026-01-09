mergeInto(LibraryManager.library, {
    // ✅ Cho ImageEditPopup (image files)
    OpenFilePicker: function() {
        console.log('[jslib] OpenFilePicker called - Opening image file picker');
        
        var input = document.createElement('input');
        input.type = 'file';
        input.accept = '.jpg,.jpeg,.png,.gif,.bmp,.tiff,.webp'; // Image files
        input.style.display = 'none';
        
        input.onchange = function(e) {
            var file = e.target.files[0];
            if (file) {
                console.log('Image file selected:', file.name, 'Size:', file.size);
                
                var reader = new FileReader();
                reader.onload = function(event) {
                    var arrayBuffer = event.target.result;
                    var uint8Array = new Uint8Array(arrayBuffer);
                    var base64String = btoa(String.fromCharCode.apply(null, uint8Array));
                    
                    // Gửi về ImageEditPopup
                    SendMessage('ImageEditPopup', 'OnImageFileSelected', base64String + '|' + file.name);
                };
                
                reader.readAsArrayBuffer(file);
            }
            
            document.body.removeChild(input);
        };
        
        document.body.appendChild(input);
        input.click();
    },
    
    // ✅ Cho Model3DEditPopup (GLB files)  
    OpenGLBFilePicker: function() {
        console.log('[jslib] OpenGLBFilePicker called - Opening GLB file picker');
        
        var input = document.createElement('input');
        input.type = 'file';
        input.accept = '.glb,.gltf'; // GLB/GLTF files
        input.style.display = 'none';
        
        input.onchange = function(e) {
            var file = e.target.files[0];
            if (file) {
                console.log('GLB file selected:', file.name, 'Size:', file.size);
                
                var reader = new FileReader();
                reader.onload = function(event) {
                    var arrayBuffer = event.target.result;
                    var uint8Array = new Uint8Array(arrayBuffer);
                    var base64String = btoa(String.fromCharCode.apply(null, uint8Array));
                    
                    // Gửi về Model3DEditPopup
                    SendMessage('Model3DEditPopup', 'OnGLBFileSelected', base64String + '|' + file.name);
                };
                
                reader.readAsArrayBuffer(file);
            }
            
            document.body.removeChild(input);
        };
        
        document.body.appendChild(input);
        input.click();
    }
});
