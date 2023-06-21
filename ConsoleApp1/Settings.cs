using System.Numerics;
using Vortice.DXGI;
using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using YamlDotNet.Serialization.NodeDeserializers;

namespace ConsoleApp1;

public class Settings
{
    interface ISettingsGroup<T>
    {
        static T CreateDefault() => throw new NotImplementedException();

        void ValidateAndCorrect();
    }

    public class WindowS : ISettingsGroup<WindowS>
    {
        public int Width { get; set; }
        public int Height { get; set; }
        public int PositionX { get; set; }
        public int PositionY { get; set; }

        // Some tiny 16:9
        public const int DefaultWidth = 854;
        public const int DefaultHeight = 480;
        // Small offset for window decorations
        public const int DefaultPositionX = 80;
        public const int DefaultPositionY = 80;

        public static WindowS CreateDefault()
        {
            return new WindowS
            {
                Width = DefaultWidth,
                Height = DefaultHeight,
                PositionX = DefaultPositionX,
                PositionY = DefaultPositionY,
            };
        }

        public void ValidateAndCorrect()
        {
            Width = Math.Clamp(Width, 100, 10000);
            Height = Math.Clamp(Height, 100, 10000);
            PositionX = Math.Max(PositionX, -(Width - 40));
            PositionY = Math.Max(PositionY, -(Height - 40));
        }
    }
    public WindowS Window { get; set; }

    public class GraphicsS : ISettingsGroup<GraphicsS>
    {
        public Format BackBufferFormat { get; set; }
        public Format DepthStencilFormat { get; set; }
        public int BackBufferCount { get; set; }

        public const Format DefaultBackBufferFormat = Format.R8G8B8A8_UNorm;
        public const Format DefaultDepthStencilFormat = Format.D32_Float;
        public const int DefaultBackBufferCount = 3;

        public static GraphicsS CreateDefault()
        {
            return new GraphicsS
            {
                BackBufferFormat = DefaultBackBufferFormat,
                DepthStencilFormat = DefaultDepthStencilFormat,
                BackBufferCount = DefaultBackBufferCount,
            };
        }
        public void ValidateAndCorrect()
        {
            BackBufferCount = Math.Clamp(BackBufferCount, 2, 8);
        }
    }
    public GraphicsS Graphics { get; set; }

    public class StateS : ISettingsGroup<StateS>
    {
        public Vector3 CameraPosition;
        public Vector3 CameraForward;

        public static Vector3 DefaultCameraPosition = Vector3.Zero;
        public static Vector3 DefaultCameraForward = new Vector3(0.0f, 0.0f, 1.0f);

        public static StateS CreateDefault()
        {
            return new StateS
            {
                CameraPosition = DefaultCameraPosition,
                CameraForward = DefaultCameraForward,
            };
        }
        public void ValidateAndCorrect() { }
    }
    StateS State;

    public Settings()
    {
        Window = WindowS.CreateDefault();
        Graphics = GraphicsS.CreateDefault();
        State = StateS.CreateDefault();
    }

    public void Save()
    {
        var yaml = new SerializerBuilder().WithNamingConvention(CamelCaseNamingConvention.Instance).Build().Serialize(this);
        using (var writer = new StreamWriter("config.yaml"))
            writer.Write(yaml);
    }

    public static Settings? Load()
    {
        if (!File.Exists("config.yaml"))
            return null;

        String yaml;
        using (var reader = new StreamReader("config.yaml"))
            yaml = reader.ReadToEnd();

        var settings = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build()
            .Deserialize<Settings>(yaml);

        settings.Window.ValidateAndCorrect();
        settings.Graphics.ValidateAndCorrect();
        settings.State.ValidateAndCorrect();

        return settings;
    }
}