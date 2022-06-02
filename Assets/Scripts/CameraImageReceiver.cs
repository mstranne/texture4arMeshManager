using System;
using System.Collections;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;


//thanks to https://github.com/dominikganghofer/ar2gh/tree/master/UnityProject/ARDataStreamer
public class CameraImageReceiver : MonoBehaviour
{

    float near = 0.2f;
    float far = 5f;

    public ARCameraManager _cameraManager = null;
    public AROcclusionManager _occlusionManager = null;
    private Texture2D _receivedTexture;
    private Texture2D _receivedDepthTexture;
    private Texture2D _rbgaTexture;
    private Texture2D _greyTexture;

    public void Start()
    {

    }

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

    public void TryGetLatestCameraAndDepthImage(Action<Texture2D, Texture2D> callback)
    {
        if (!_cameraManager.TryAcquireLatestCpuImage(out var image))
        {
            //callback.Invoke(_fallbackTexture);
            Debug.Log("failed aquire image");
            return;
        }

        if (!_occlusionManager.TryAcquireEnvironmentDepthCpuImage(out var depth))
        {
            //callback.Invoke(_fallbackTexture);
            Debug.Log("failed aquire depth image");
            return;
        }

        StartCoroutine(ProcessImageAndDepth(image, depth, callback));
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

    private IEnumerator ProcessImageAndDepth(XRCpuImage image, XRCpuImage depth, Action<Texture2D, Texture2D> callback)
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


        //4 depth        
        request = depth.ConvertAsync(new XRCpuImage.ConversionParams()
        {
            inputRect = new RectInt(0, 0, depth.width, depth.height),
            outputDimensions = new Vector2Int(depth.width, depth.height),
            outputFormat = depth.format.AsTextureFormat(),
            transformation = XRCpuImage.Transformation.MirrorX
        });

        while (!request.status.IsDone())
            yield return null;

        if (request.status != XRCpuImage.AsyncConversionStatus.Ready)
        {
            request.Dispose();
            yield break;
        }

        rawData = request.GetData<byte>();

        if (_receivedDepthTexture == null)
        {
            _receivedDepthTexture = new Texture2D(
                request.conversionParams.outputDimensions.x,
                request.conversionParams.outputDimensions.y,
                request.conversionParams.outputFormat,
                false);
        }

        _receivedDepthTexture.LoadRawTextureData(rawData);
        _receivedDepthTexture.Apply();

        if (_greyTexture == null)
            _greyTexture = new Texture2D(_receivedDepthTexture.width, _receivedDepthTexture.height, TextureFormat.BGRA32,
                false);

        ConvertFloatToGrayScale(_receivedDepthTexture, _greyTexture);

        callback.Invoke(_rbgaTexture, _greyTexture);
        request.Dispose();
    }

    void ConvertFloatToGrayScale(Texture2D txFloat, Texture2D txGray)
    {

        //Conversion of grayscale from near to far value
        int length = txGray.width * txGray.height;
        Color[] depthPixels = txFloat.GetPixels();
        Color[] colorPixels = txGray.GetPixels();
        
        for (int index = 0; index < length; index++)
        {

            var value = (depthPixels[index].r - near) / (far - near);

            colorPixels[index].r = value;
            colorPixels[index].g = value;
            colorPixels[index].b = value;
            colorPixels[index].a = 1;
        }
        txGray.SetPixels(colorPixels);
        txGray.Apply();
    }

    public void enableOcclution(bool enable)
    {
        _occlusionManager.enabled = enabled;
    }

}
