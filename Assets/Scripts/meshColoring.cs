using System;
using System.Collections;
using System.Collections.Generic;
using ar2gh.camera;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;

public class meshColoring : MonoBehaviour
{
    public CameraImageReceiver _cameraImageReceiver = null;
    public Camera _camera = null;

    public ARMeshManager mesh_manager = null;
    public Text btn_text = null;
    public Material visible = null;
    public Material invisible = null;
    public GameObject ProjectorPrefab = null;

    private List<Vector3> cam_poses_trans = new List<Vector3>();
    private List<Quaternion> cam_poses_rot = new List<Quaternion>();
    private List<Texture2D> cam_textures = new List<Texture2D>();
    private List<GameObject> projectors = new List<GameObject>();

    public GameObject rjctr = null;
    public Texture2D txtr = null;

    public bool use_Projectors = true;
    public bool use_updates = false;

    private List<GameObject> curr_meshes = new List<GameObject>();

    // Start is called before the first frame update
    void Start()
    {
        if(mesh_manager == null)
            Debug.LogError("mesh manager null");

        if(!use_Projectors && use_updates)
            mesh_manager.meshesChanged += ARMeshChanged;
    }

    private void ARMeshChanged(ARMeshesChangedEventArgs obj)
    {
        if (obj.updated.Count > 0)
            Debug.Log("mname " + obj.updated[0].mesh.name);
        _cameraImageReceiver.TryGetLatestCameraImage(texture2D =>
                       OnCameraImageReceived(obj.added, obj.updated, texture2D));
    }


    float lastTime = 0;
    // Update is called once per frame
    void Update()
    {
        if (!use_updates && Time.time - lastTime > 5 && !showing)
        {
            foreach(var t in cam_poses_trans)
            {
                if (Vector3.Distance(_camera.transform.position, t) < 0.5f)
                    return;
            }

            Vector3 pos = _camera.transform.position;
            Quaternion q = _camera.transform.rotation;
            _cameraImageReceiver.TryGetLatestCameraImage(texture2D =>
                       AddProjector(pos, q, texture2D));
        } 
    }

    public IEnumerator UpdateMeshVertexCols()
    {
        yield return new WaitForEndOfFrame();
    }

    private void OnCameraImageReceived(List<MeshFilter> Added, List<MeshFilter> Updated, Texture2D camTexture)
    {
        //Debug.Log("Updated cnt2: " + Updated.Count);
        if (Updated.Count > 0)
            Debug.Log("mname 2" + Updated[0].mesh.name);
        WriteCameraImageColors(_camera, camTexture, Added, Updated);
        //var data = MeshSerializer.GenerateMeshData(meshes);
        //DataReadyEvent?.Invoke(data);
    }

    public void WriteCameraImageColors(Camera camera, Texture2D camTexture, List<MeshFilter> Added, List<MeshFilter> Updated)
    {
        //Debug.Log("Updated cnt3: " + Updated.Count);
        if (Updated.Count > 0)
            Debug.Log("mname 3" + Updated[0].mesh.name);
        for (var iMesh = 0; iMesh < Added.Count; iMesh++)
        {
            var m = Added[iMesh].mesh;
            var colors = new Color[m.vertices.Length];
            for (var i = 0; i < m.vertices.Length; i++)
            {
                var vertex = m.vertices[i];
                colors[i] = GetColorAtWorldPosition(vertex, camTexture, camera);
            }
            Debug.Log("cer cnt: " + m.vertices.Length);
            Added[iMesh].mesh.colors = colors;
        }

        for (var iMesh = 0; iMesh < Updated.Count; iMesh++)
        {
            var m = Updated[iMesh].mesh;
            var colors = new Color[m.vertices.Length];
            for (var i = 0; i < m.vertices.Length; i++)
            {
                var vertex = m.vertices[i];
                colors[i] = GetColorAtWorldPosition(vertex, camTexture, camera);
            }

            Debug.Log("cer cnt updtd: " + m.vertices.Length);
            Updated[iMesh].mesh.colors = colors;
        }

        //Debug.Log("finisged");
    }

    private static Color GetColorAtWorldPosition(Vector3 worldPosition, Texture2D texture, Camera camera)
    {
        var screenPosition = camera.WorldToScreenPoint(worldPosition);
        if (screenPosition.x < 0 || screenPosition.x > Screen.width)
            return Color.black;
        if (screenPosition.y < 0 || screenPosition.y > Screen.height)
            return Color.black;

        Debug.Log("set col");

        var wTextureToScreen = texture.width / (1f * Screen.width);
        var hTextureToScreen = texture.height / (1f * Screen.height);

        return texture.GetPixel((int)(wTextureToScreen * screenPosition.x),
            (int)(hTextureToScreen * screenPosition.y));
    }

    bool showing = false;
    public void showMesh()
    {

        // rjctr.GetComponent<Projector>().material.SetTexture("_ShadowTex", txtr); 

        showing = !showing;
        if (showing)
        {
            btn_text.text = "stop showing";
            if (use_Projectors)
            {
                mesh_manager.meshPrefab.GetComponent<MeshRenderer>().material = visible;
                Debug.Log("mesh material changed");

                for (int idx = 0; idx < cam_poses_trans.Count; idx++)
                {
                    var proj = Instantiate(ProjectorPrefab, cam_poses_trans[idx], cam_poses_rot[idx]);
                    proj.GetComponent<Projector>().material.SetTexture("_ShadowTex", cam_textures[idx]);
                    projectors.Add(proj);
                }

                Debug.Log(cam_poses_trans.Count + " projectors set");
            }
            else
            {
                IList<MeshFilter> meshes = mesh_manager.meshes;

                foreach(var m in meshes)
                {
                    GameObject newMesh = new GameObject(m.name);
                    MeshFilter mFilter = newMesh.AddComponent<MeshFilter>();
                    MeshRenderer mRender = newMesh.AddComponent<MeshRenderer>();
                    mFilter.mesh = m.mesh;
                    mRender.material = visible;

                    curr_meshes.Add(newMesh);
                }

                mesh_manager.DestroyAllMeshes();
                cam_poses_rot.Clear();
                cam_poses_trans.Clear();
                cam_textures.Clear();
                mesh_manager.enabled = false;
            }
        }
        else
        {
            btn_text.text = "show mesh";
            if (use_Projectors)
            {
                mesh_manager.meshPrefab.GetComponent<MeshRenderer>().material = invisible;
                Debug.Log("mesh material changed");
                foreach (var p in projectors)
                    Destroy(p);

                projectors.Clear();
            }
            else
            {

                foreach(var obj in curr_meshes)
                {
                    Destroy(obj);
                }

                mesh_manager.enabled = true;
            }
        }
    }

    public void AddProjector(Vector3 pose_t, Quaternion pose_q, Texture2D camTexture)
    {
        cam_poses_trans.Add(pose_t);
        cam_poses_rot.Add(pose_q);
        cam_textures.Add(camTexture);
    }
}