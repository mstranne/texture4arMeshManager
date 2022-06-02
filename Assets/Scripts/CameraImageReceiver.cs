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
    private Texture2D _depthPntsTexture;

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

        if (_depthPntsTexture == null)
            _depthPntsTexture = new Texture2D(_receivedDepthTexture.width, _receivedDepthTexture.height, TextureFormat.RGBAFloat,
                false);

        //ConvertFloatTo3DPoints(_receivedDepthTexture, _depthPntsTexture);

        //callback.Invoke(_rbgaTexture, _depthPntsTexture);
        callback.Invoke(_rbgaTexture, _receivedDepthTexture);
        request.Dispose();
    }

    void ConvertFloatTo3DPoints(Texture2D txFloat, Texture2D txPnts)
    {
        int width_depth = txFloat.width;
        int height_depth = txFloat.height;
        int width_camera = _rbgaTexture.width;

        XRCameraIntrinsics intrinsic;
        _cameraManager.TryGetIntrinsics(out intrinsic);
        print("intrinsics:" + intrinsic);

        float ratio = (float)width_depth / (float)width_camera;
        float fx = intrinsic.focalLength.x * ratio;
        float fy = intrinsic.focalLength.y * ratio;

        float cx = intrinsic.principalPoint.x * ratio;
        float cy = intrinsic.principalPoint.y * ratio;

        //Conversion of grayscale from near to far value
        int length = txFloat.width * txFloat.height;
        Color[] depthPixels = txFloat.GetPixels();
        Color[] posPixels = txFloat.GetPixels();

        float depth = 0;
        int index_dst = 0;
        for (int depth_y = 0; depth_y < height_depth; depth_y++)
        {
            index_dst = depth_y * width_depth;
            for (int depth_x = 0; depth_x < width_depth; depth_x++)
            {
                //colors[index_dst] = m_CameraTexture.GetPixelBilinear((float)depth_x / (width_depth), (float)depth_y / (height_depth));

                depth = depthPixels[index_dst].r;
                if (depth > near && depth < far)
                {
                    Vector3 pos = new Vector3(-depth * (depth_x - cx) / fx, -depth * (depth_y - cy) / fy, depth);

                    pos = _cameraManager.transform.rotation * pos + _cameraManager.transform.position;
                    posPixels[index_dst].r = pos.x;
                    posPixels[index_dst].g = pos.y;
                    posPixels[index_dst].b = pos.z;
                    
                }
                else
                {
                    posPixels[index_dst].r = -999;
                    posPixels[index_dst].g = 0;
                    posPixels[index_dst].b = 0;
                }
                //index_dst++;
            }
        }

        txPnts.SetPixels(posPixels);
        txPnts.Apply();
    }

    public void enableOcclution(bool enable)
    {
        
        if(enable)
            _occlusionManager.requestedEnvironmentDepthMode = UnityEngine.XR.ARSubsystems.EnvironmentDepthMode.Medium;
        else
            _occlusionManager.requestedEnvironmentDepthMode = UnityEngine.XR.ARSubsystems.EnvironmentDepthMode.Disabled;
    }

}
