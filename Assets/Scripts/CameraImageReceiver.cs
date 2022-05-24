using System;
using System.Collections;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;


//thanks to https://github.com/dominikganghofer/ar2gh/tree/master/UnityProject/ARDataStreamer
public class CameraImageReceiver : MonoBehaviour
{
    public ARCameraManager _cameraManager = null;
    private Texture2D _receivedTexture;
    private Texture2D _rbgaTexture;

    public void TryGetLatestCameraImage(Action<Texture2D> callback)
    {
        if (!_cameraManager.TryAcquireLatestCpuImage(out var image))
        {
            //callback.Invoke(_fallbackTexture);
            Debug.Log("failed aquire image");
            return;
        }

        StartCoroutine(ProcessImage(image, callback));
        image.Dispose();
    }

    private IEnumerator ProcessImage(XRCpuImage image, Action<Texture2D> callback)
    {
        var request = image.ConvertAsync(new XRCpuImage.ConversionParams()
        {
            inputRect = new RectInt(0, 0, image.width, image.height),
            outputDimensions = new Vector2Int(image.width, image.height),
            outputFormat = TextureFormat.RGBA32,
            transformation = XRCpuImage.Transformation.MirrorX
        });

        while (!request.status.IsDone())
            yield return null;

        if (request.status != XRCpuImage.AsyncConversionStatus.Ready)
        {
            request.Dispose();
            yield break;
        }

        var rawData = request.GetData<byte>();

        if (_receivedTexture == null)
        {
            _receivedTexture = new Texture2D(
                request.conversionParams.outputDimensions.x,
                request.conversionParams.outputDimensions.y,
                request.conversionParams.outputFormat,
                false);
        }

        _receivedTexture.LoadRawTextureData(rawData);
        _receivedTexture.Apply();

        // convert to rgba texture
        if (_rbgaTexture == null)
            _rbgaTexture = new Texture2D(_receivedTexture.width, _receivedTexture.height, TextureFormat.RGBA32,
                false);

        _rbgaTexture.SetPixels(_receivedTexture.GetPixels());
        _rbgaTexture.Apply();
        callback.Invoke(_rbgaTexture);
        request.Dispose();
    }
}
