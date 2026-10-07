using Godot;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

public class AsyncImageLoader
{
    private const int MaximumConcurrentDecodes = 3;

    private readonly SemaphoreSlim decodeSlots = new SemaphoreSlim(MaximumConcurrentDecodes);
    private readonly ConcurrentQueue<(string Key, Image Image)> decodedImages = new ConcurrentQueue<(string, Image)>();
    private readonly Dictionary<string, List<Action<ImageTexture>>> callbacksByKey = new Dictionary<string, List<Action<ImageTexture>>>();

    public static string KeyFor(string path, int maximumWidth) => $"{maximumWidth}|{path}";

    public bool IsPending(string path, int maximumWidth) => callbacksByKey.ContainsKey(KeyFor(path, maximumWidth));

    public void Request(string path, int maximumWidth, Action<ImageTexture> onReady)
    {
        string key = KeyFor(path, maximumWidth);

        if (callbacksByKey.TryGetValue(key, out List<Action<ImageTexture>> waitingCallbacks))
        {
            waitingCallbacks.Add(onReady);
            return;
        }

        callbacksByKey[key] = new List<Action<ImageTexture>> { onReady };

        Task.Run(async () =>
        {
            await decodeSlots.WaitAsync();

            Image image = null;

            try
            {
                image = Decode(path);

                if (image != null && maximumWidth > 0 && image.GetWidth() > maximumWidth)
                {
                    int scaledHeight = Math.Max(1, (int)Math.Round((double)image.GetHeight() * maximumWidth / image.GetWidth()));
                    image.Resize(maximumWidth, scaledHeight, Image.Interpolation.Bilinear);
                }
            }

            catch (Exception exception)
            {
                GD.PrintErr($"Image decode failed for {path}: {exception.Message}");
                image = null;
            }

            finally
            {
                decodeSlots.Release();
            }

            decodedImages.Enqueue((key, image));
        });
    }

    public void Pump(double budgetMilliseconds)
    {
        var frameBudget = Stopwatch.StartNew();

        while (decodedImages.TryDequeue(out var decoded))
        {
            ImageTexture texture = decoded.Image != null ? ImageTexture.CreateFromImage(decoded.Image) : null;

            if (callbacksByKey.Remove(decoded.Key, out List<Action<ImageTexture>> callbacks))
            {
                foreach (Action<ImageTexture> callback in callbacks)
                {
                    callback(texture);
                }
            }

            if (frameBudget.Elapsed.TotalMilliseconds >= budgetMilliseconds)
            {
                break;
            }
        }
    }

    public static Image Decode(string path)
    {
        if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path))
        {
            return null;
        }

        byte[] fileData = System.IO.File.ReadAllBytes(path);

        if (fileData.Length < 12)
        {
            return null;
        }

        var image = new Image();
        Error result = Error.Failed;

        if (fileData[0] == 0x89 && fileData[1] == 0x50 && fileData[2] == 0x4E && fileData[3] == 0x47)
        {
            result = image.LoadPngFromBuffer(fileData);
        }

        else if (fileData[0] == 0xFF && fileData[1] == 0xD8 && fileData[2] == 0xFF)
        {
            result = image.LoadJpgFromBuffer(fileData);
        }

        else if (fileData[0] == 0x52 && fileData[1] == 0x49 && fileData[2] == 0x46 && fileData[3] == 0x46 &&
                 fileData[8] == 0x57 && fileData[9] == 0x45 && fileData[10] == 0x42 && fileData[11] == 0x50)
        {
            result = image.LoadWebpFromBuffer(fileData);
        }

        return result == Error.Ok && !image.IsEmpty() ? image : null;
    }
}
