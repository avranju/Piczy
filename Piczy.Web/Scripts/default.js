(function () {
    "use strict";

    $(function () {
        $("#btnUpload").click(function (e) {
            e.preventDefault();
            $("#pics").click();
        });

        // File status enumeration
        var FileStatus = Object.freeze({
            Error: 0,
            Uploading: 1,
            Processing: 2,
            Processed: 3
        });

        var tableBody = $("#tableBody");

        // load the existing photos
        loadPhotos();

        function loadPhotos() {
            $.getJSON("PiczyService.ashx", function (items) {
                // clear table body
                if(items.length > 0) {
                    tableBody.html("");
                }

                items.forEach(function (item) {
                    // create thumbnail and row in the table
                    var row = document.createElement("tr");
                    var preview = document.createElement("td");
                    var imageName = document.createElement("td");
                    var fileSize = document.createElement("td");
                    var status = document.createElement("td");

                    var img = document.createElement("img");
                    img.width = "100";
                    img.src = item.BlobUrl;

                    // if the item has been completely processed make
                    // this a link
                    if (item.Status === FileStatus.Processed) {
                        $(img).addClass("previewImage").click(function () {
                            window.open("Preview.aspx?BlobUrl=" + encodeURIComponent(item.BlobUrl));
                        });
                    }

                    preview.appendChild(img);

                    imageName.textContent = item.RowKey;
                    fileSize.textContent = item.FileSize;
                    status.textContent = getStatusText(item.Status);

                    row.appendChild(preview);
                    row.appendChild(imageName);
                    row.appendChild(fileSize);
                    row.appendChild(status);

                    tableBody.append(row);
                });
            });
        }

        function getStatusText(status) {
            switch (status) {
                case 0:
                    return "Error";
                case 1:
                    return "Uploading";
                case 2:
                    return "Processing";
                case 3:
                    return "Processed";
            }
        }

        $("#pics").bind("change", function (e) {
            var filesList = this.files;
            if (filesList.length === 0) {
                return;
            }

            // if no-items-msg is in the table, remove it
            var noItems = document.querySelector("#no-items-msg");
            if (noItems) {
                noItems.parentNode.removeChild(noItems);
            }

            for (var i = 0; i < filesList.length; ++i) {
                var file = filesList[i];

                // create thumbnail and row in the table
                var row = document.createElement("tr");
                var preview = document.createElement("td");
                var imageName = document.createElement("td");
                var fileSize = document.createElement("td");
                var status = document.createElement("td");
                var progress = document.createElement("progress");

                status.appendChild(progress);
                row.appendChild(preview);
                row.appendChild(imageName);
                row.appendChild(fileSize);
                row.appendChild(status);
                tableBody.append(row);

                var img = document.createElement("img");
                img.width = "100";
                img.src = URL.createObjectURL(file);
                img.onload = function () {
                    URL.revokeObjectURL(this.src);
                };
                preview.appendChild(img);

                imageName.textContent = file.name;
                fileSize.textContent = file.size;

                // upload file to server
                uploadFile(file, status, progress);
            }
        });

        function uploadFile(file, status, progress) {
            var xhr = new XMLHttpRequest();
            xhr.open("POST", "Upload.ashx?fileName=" + encodeURIComponent(file.name) +
                "&contentType=" + encodeURIComponent(file.type) +
                "&fileSize=" + file.size);
            xhr.setRequestHeader("Content-Type", file.type);

            xhr.onload = function () {
                status.textContent = "Upload complete";
            };
            xhr.onerror = function () {
                status.textContent = "Upload failed.";
            };
            xhr.upload.onprogress = function (event) {
                progress.value = event.loaded / event.total;
            }
            xhr.upload.onloadstart = function (event) {
                progress.setAttribute("value", "0");
            }

            xhr.send(file);
        }
    });
})();
