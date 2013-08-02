var mediaPlayer = 
{
    initFunction : function (window, sourceUrl)
    {
        var myPlayer = new PlayerFramework.Player(window,
        {
            mediaPluginFallbackOrder: ["VideoElementMediaPlugin", "SilverlightMediaPlugin"],
            width: "480px",
            height: "320px",
            sources:
            [
                {
                    src: sourceUrl,
                    type: 'video/mp4;'
                }
            ]
        });
    }
}