/// <reference path="jquery-1.8.2.js" />
/// <reference path="_references.js" />

var maxRetries = 3;
var blockLength = 1048576;
var numberOfBlocks = 1;
var currentChunk = 1;
var retryAfterSeconds = 3;

$(document).ready(function ()
{
    $(document).on("click", "#fileUpload", beginUpload);
    $(document).on("click", "#saveDetails", saveDetails);
    $("#detailsPanel").hide();
    $("#progressBar").progressbar(0);
});

var beginUpload = function ()
{
    var fileControl = document.getElementById("selectFile");
    if (fileControl.files.length > 0)
    {
        for (var i = 0; i < fileControl.files.length; i++)
        {
            uploadMetaData(fileControl.files[i], i);
        }
    }
}

var saveDetails = function ()
{
    var dataPost = {
        "Title": $("#title").val(),
        "AssetId": $("#assetId").val()
    }
    $.ajax({
        type: "POST",
        async: false,
        contentType: "application/json",
        data: JSON.stringify(dataPost),
        url: "/Media/Save"
    }).done(function (state)
    {
        if (state.Saved == true)
        {
            displayStatusMessage("Saved Successfully");
            $("#detailsPanel").hide();
            mediaPlayer.initFunction("videoDisplayPane", state.StreamingUrl);
        }
        else
        {
            displayStatusMessage("Saved Failed");
        }
    });
}

var uploadMetaData = function (file, index)
{
    var size = file.size;
    numberOfBlocks = Math.ceil(file.size / blockLength);
    var name = file.name;
    currentChunk = 1;

    $.ajax({
        type: "POST",
        async: false,
        url: "/Media/SetMetadata?blocksCount=" + numberOfBlocks + "&fileName=" + name + "&fileSize=" + size,
    }).done(function (state)
    {
        if (state === true)
        {
            $("#fileUpload").hide();
            displayStatusMessage("Starting Upload");
            sendFile(file, blockLength);
        }
    }).fail(function ()
    {
        $("#fileUpload").show()
        displayStatusMessage("Failed to send MetaData");
    });

}

var sendFile = function (file, chunkSize)
{
    var start = 0,
        end = Math.min(chunkSize, file.size),
        retryCount = 0,
        sendNextChunk, fileChunk;
    displayStatusMessage("");

    sendNextChunk = function ()
    {
        fileChunk = new FormData();

        if (file.slice)
        {
            fileChunk.append('Slice', file.slice(start, end));
        }
        else if (file.webkitSlice)
        {
            fileChunk.append('Slice', file.webkitSlice(start, end));
        }
        else if (file.mozSlice)
        {
            fileChunk.append('Slice', file.mozSlice(start, end));
        }
        else
        {
            displayStatusMessage(operationType.UNSUPPORTED_BROWSER);
            return;
        }
        jqxhr = $.ajax({
            async: true,
            url: ('/Media/UploadChunk?id=' + currentChunk),
            data: fileChunk,
            cache: false,
            contentType: false,
            processData: false,
            type: 'POST'
        }).fail(function (request, error)
        {
            if (error !== 'abort' && retryCount < maxRetries)
            {
                ++retryCount;
                setTimeout(sendNextChunk, retryAfterSeconds * 1000);
            }

            if (error === 'abort')
            {
                displayStatusMessage("Aborted");
            }
            else
            {
                if (retryCount === maxRetries)
                {
                    displayStatusMessage("Upload timed out.");
                    resetControls();
                    uploader = null;
                }
                else
                {
                    displayStatusMessage("Resuming Upload.");
                }
            }

            return;
        }).done(function (notice)
        {
            if (notice.error || notice.isLastBlock)
            {
                displayStatusMessage(notice.message);
                if (notice.isLastBlock)
                {
                    $("#assetId").val(notice.assetId);
                    $("#detailsPanel").show();
                }
                return;
            }
            ++currentChunk;
            start = (currentChunk - 1) * blockLength;
            end = Math.min(currentChunk * blockLength, file.size);
            retryCount = 0;
            updateProgress();
            if (currentChunk <= numberOfBlocks)
            {
                sendNextChunk();
            }
        });
    }
    sendNextChunk();
}

var displayStatusMessage = function (message)
{
    $("#statusMessage").text(message);
}

var updateProgress = function ()
{
    var progress = currentChunk / numberOfBlocks * 100;
    if (progress <= 100)
    {
        $("#progressBar").progressbar("option", "value", parseInt(progress));
        displayStatusMessage("Uploaded " + parseInt(progress) + "%");
    }

}