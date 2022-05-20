using System;
using System.Collections;
using System.Collections.Generic;
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

    //Save poses and cam textures for texturing later on
    private List<Vector3> cam_poses_trans = new List<Vector3>();
    private List<Quaternion> cam_poses_rot = new List<Quaternion>();
    private List<Texture2D> cam_textures = new List<Texture2D>();
    private List<GameObject> projectors = new List<GameObject>();

    public GameObject rjctr = null;
    public Texture2D txtr = null;

    //using Projectors for easy texturing
    public bool use_Projectors = true;

    //texture directly with mesh manager updates (todo not working atm)
    public bool use_updates = false;

    //list of meshes when not textured with runnning MeshManager instance
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
        if (!use_updates && Time.time - lastTime > 5 && !showing)   //check all 5 sec
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
                Color? n_cal = GetColorAtWorldPosition(vertex, camTexture, camera);
                if(n_cal != null)
                    colors[i] = n_cal.Value;
            }

            Debug.Log("cer cnt updtd: " + m.vertices.Length);
            Updated[iMesh].mesh.colors = colors;
        }

        //Debug.Log("finisged");
    }

    private static Color? GetColorAtWorldPosition(Vector3 worldPosition, Texture2D texture, Camera camera)
    {
        var screenPosition = camera.WorldToScreenPoint(worldPosition);
        if (screenPosition.x < 0 || screenPosition.x > Screen.width)
            return null;
        if (screenPosition.y < 0 || screenPosition.y > Screen.height)
            return null;

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

                Camera cam = new Camera();
                cam.CopyFrom(Camera.main);
                foreach (var m in meshes)
                {
                    GameObject newMesh = new GameObject(m.name);
                    MeshFilter mFilter = newMesh.AddComponent<MeshFilter>();
                    MeshRenderer mRender = newMesh.AddComponent<MeshRenderer>();
                    mFilter.mesh = m.mesh;
                    mRender.material = visible;

                    Mesh mesh_ = mFilter.mesh;
                    var colors = new Color?[mesh_.vertices.Length];
                    for (int idx = 0; idx < cam_poses_trans.Count; idx++)
                    {
                        cam.transform.position = cam_poses_trans[idx];
                        cam.transform.rotation = cam_poses_rot[idx];

                        for (var i = 0; i < mesh_.vertices.Length; i++)
                        {
                            var vertex = mesh_.vertices[i];
                            Color? new_cal = GetColorAtWorldPosition(vertex, cam_textures[idx], cam);
                            if (new_cal != null)
                            {
                                if (colors[i] == null)
                                    colors[i] = new_cal.Value;
                                else
                                    Debug.Log("todo already set");
                            } 
                        }
                    }
                    Debug.Log("cer cnt: " + mesh_.vertices.Length);

                    mesh_.colors = new Color[colors.Length];
                    for (int idx = 0; idx < colors.Length; idx++)
                        mesh_.colors[idx] = colors[idx].Value;
                    
                    curr_meshes.Add(newMesh);
                }
                Destroy(cam);

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