namespace Pico4SAFTExtTrackingModule.PicoConnectors.ConfigChecker.PicoConnect;

public class Config
{
    public Video video { get; set; }
    public Audio audio { get; set; }
    public General general { get; set; }
    public Lab lab { get; set; }
    public Game game { get; set; }
    public Desktop desktop { get; set; }
}

public class Video
{
    public string resolution { get; set; }
    public Bitrate bitrate { get; set; }
    public bool autoBitrate { get; set; }
    public bool refreshRate90Hz { get; set; }
    public bool frameBuffer { get; set; }
    public string codec { get; set; }
    public bool asw { get; set; }
    public int sharpenRate { get; set; }
}

public class Bitrate
{
    public int smooth { get; set; }
    public int sd { get; set; }
    public int hd { get; set; }
    public int ultra { get; set; }
}

public class Audio
{
    public bool mic { get; set; }
    public int volume { get; set; }
    public string output { get; set; }
    public int latency { get; set; }
}

public class General
{
    public bool autoConnect { get; set; }
    public string language { get; set; }
    public bool loginItem { get; set; }
    public bool closeToTray { get; set; }
}

public class Lab
{
    public bool quic { get; set; }
    public bool superResolution { get; set; }
    public int gamma { get; set; }
    public int faceTrackingMode { get; set; }
    public int faceTrackingTransferProtocol { get; set; }
    public bool bodyTracking { get; set; }
    public int controllerSensitivity { get; set; }
}

public class Game
{
    public string resolution { get; set; }
    public GameBitrate bitrate { get; set; }
    public bool autoBitrate { get; set; }
    public bool refreshRate90Hz { get; set; }
    public bool frameBuffer { get; set; }
    public string codec { get; set; }
    public bool asw { get; set; }
    public int sharpenRate { get; set; }
    public bool superResolution { get; set; }
    public int gamma { get; set; }
    public int refreshRate { get; set; }
}

public class GameBitrate
{
    public int smooth { get; set; }
    public int sd { get; set; }
    public int hd { get; set; }
    public int ultra { get; set; }
    public int uhd { get; set; }
}

public class Desktop
{
    public int sharpenRate { get; set; }
}
