using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
    public Material texture_mat = null;

    //Save poses and cam textures for texturing later on
    private List<Vector3> cam_poses_trans = new List<Vector3>();
    private List<Quaternion> cam_poses_rot = new List<Quaternion>();
    private List<Texture2D> cam_textures = new List<Texture2D>();
    private List<Texture2D> depth_textures = new List<Texture2D>();
    private List<GameObject> projectors = new List<GameObject>();


    //texture directly with mesh manager updates (todo not working atm)
    public bool use_updates = false;
    public bool subdivide_mesh = false;
    public bool create_texture_atlas = false;
    public bool image_auto = false;
    public bool use_depth = true;
    public GameObject TakeImg = null;

    //list of meshes when not textured with runnning MeshManager instance
    private List<GameObject> curr_meshes = new List<GameObject>();


    //todo 
    public GameObject rayHitter = null;
    public Text btn_ray_txt = null;
    bool move = true;

    // Start is called before the first frame update
    void Start()
    {
        if (mesh_manager == null)
            Debug.LogError("mesh manager null");

        if(use_updates)
            mesh_manager.meshesChanged += ARMeshChanged;

        if (image_auto)
            TakeImg.SetActive(false);

        //GameObject obj = new GameObject("camObj");
        //MeshFilter mFilter = obj.AddComponent<MeshFilter>();
        //mFilter.mesh = m;


        if (false)
        {
           // StartCoroutine(testScript());   
        }
    }



    private void ARMeshChanged(ARMeshesChangedEventArgs obj)
    {
        if (obj.updated.Count > 0)
            Debug.Log("mname " + obj.updated[0].mesh.name);
        _cameraImageReceiver.TryGetLatestCameraImage(texture2D =>
                       OnCameraImageReceived(obj.added, obj.updated, texture2D));
    }


    RaycastHit hitData2;
    float lastTime = 0;
    // Update is called once per frame
    void Update()
    {
        if (image_auto && !use_updates && Time.time - lastTime > 5 && !showing)   //check all 5 sec
        {
            foreach(var t in cam_poses_trans)
            {
                if (Vector3.Distance(_camera.transform.position, t) < 0.25f)
                    return;
            }

            Vector3 pos = _camera.transform.position;
            Quaternion q = _camera.transform.rotation;
            _cameraImageReceiver.TryGetLatestCameraAndDepthImage((texture2D, depthTexture2D) =>
                       AddImages(pos, q, texture2D, depthTexture2D));
        }

        if (move)
        {
            Ray ray = Camera.main.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
          ;
            if (Physics.Raycast(ray, out hitData2))
            {
                rayHitter.transform.position = hitData2.point;
                return;
            }
            else
            {
                Debug.Log("ray hit nothin");
                return;
            }
        }
    }

    public void takeImage()
    {
        Vector3 pos = _camera.transform.position;
        Quaternion q = _camera.transform.rotation;
        _cameraImageReceiver.TryGetLatestCameraAndDepthImage((texture2D, depthTexture2D) =>
                   AddImages(pos, q, texture2D, depthTexture2D));
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
                Color? n_cal = GetColorAtWorldPosition(vertex, camTexture, camera);
                if (n_cal != null)
                    colors[i] = n_cal.Value;
            }
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

    private Vector2? GetScreenPositionFromWorld(Vector3 worldPosition, Texture2D texture, Camera camera)
    {
        var screenPosition = camera.WorldToScreenPoint(worldPosition);

        if (screenPosition.x < 0 || screenPosition.x > Screen.width)
            return null;
        if (screenPosition.y < 0 || screenPosition.y > Screen.height)
            return null;

        var wTextureToScreen = texture.width / (1f * Screen.width);
        var hTextureToScreen = texture.height / (1f * Screen.height);

        if (use_depth)
        {
            Color depth_val = texture.GetPixel((int)(wTextureToScreen * screenPosition.x), (int)(hTextureToScreen * screenPosition.y));
            Vector3 pos = new Vector3(depth_val.r, depth_val.g, depth_val.b);
            if(Mathf.Abs(Vector3.Distance(pos, worldPosition)) > 0.01)
            {
                Debug.Log("too far away = " +  worldPosition + " - " + depth_val);
                return null;
            }
        }
        else
        {
            Ray ray = camera.ScreenPointToRay(screenPosition);
            RaycastHit hitData;
            if (Physics.Raycast(ray, out hitData))
            {
                if (Vector3.Distance(hitData.point, worldPosition) > 0.01f)
                {
                    //Debug.Log("not visible");
                    return null;
                }
            }
            else
            {
                //Debug.Log("ray hit nothin");
                //return null;
            }
        }

        

        return new Vector2((wTextureToScreen * screenPosition.x)/texture.width, (hTextureToScreen * screenPosition.y)/texture.height);
    }

    bool showing = false;
    public void showMesh()
    {

        // rjctr.GetComponent<Projector>().material.SetTexture("_ShadowTex", txtr); 
        int idx = 0;
        showing = !showing;
        if (showing)
        {
            btn_text.text = "stop showing";
            _cameraImageReceiver.enableOcclution(!showing);
            
            IList<MeshFilter> meshes = mesh_manager.meshes;
            GameObject camObj = new GameObject("camObj");
            Camera cam = camObj.AddComponent<Camera>();
            cam.CopyFrom(_camera);

            string posefile = Path.Combine(Application.persistentDataPath, "out.txt");
            string content = "";
            for (idx = 0; idx < cam_poses_rot.Count; idx++)
            {
                byte[] bytes = cam_textures[idx].EncodeToPNG();
                var dirPath = Application.persistentDataPath + "/imgs/";
                if (!Directory.Exists(dirPath))
                {
                    Directory.CreateDirectory(dirPath);
                }
                File.WriteAllBytes(dirPath + "Image" + idx + ".png", bytes);

                bytes = depth_textures[idx].EncodeToPNG();                
                File.WriteAllBytes(dirPath + "Depth" + idx + ".png", bytes);

                content += String.Format("{0:F5};{1:F5};{2:F5} \n{3:F5};{4:F5};{5:F5};{6:F5} \n", cam_poses_trans[idx].x, cam_poses_trans[idx].y,
                        cam_poses_trans[idx].z, cam_poses_rot[idx].x, cam_poses_rot[idx].y, cam_poses_rot[idx].z, cam_poses_rot[idx].w);
            }
            File.WriteAllText(posefile, content);

            Rect[] atlas_rec = null;
            if (create_texture_atlas)
            {
                Texture2D atlas = new Texture2D(8192, 8192);
                atlas_rec = atlas.PackTextures(cam_textures.ToArray(), 0);

                texture_mat.mainTexture = atlas;
            }

            int midx = 0;

            List<CombineInstance> combine = new List<CombineInstance>();
                
            foreach (var mesh in meshes)
            {

                //yield return new WaitForEndOfFrame();

                    
                CombineInstance c = new CombineInstance();
                c.mesh = mesh.sharedMesh;
                c.transform = mesh.transform.localToWorldMatrix;
                combine.Add(c);
                    
            }

            GameObject newMesh = new GameObject("new mesh");
            MeshFilter mFilter = newMesh.AddComponent<MeshFilter>();
            MeshRenderer mRender = newMesh.AddComponent<MeshRenderer>();

            mFilter.mesh = new Mesh();
            mFilter.mesh.CombineMeshes(combine.ToArray());

            Byte[] to_write = MeshSerializer.WriteMesh(mFilter.mesh, true);
            string path = Path.Combine(Application.persistentDataPath, midx++ + ".bin");
            File.WriteAllBytes(path, to_write);

            //ObjExporter.MeshToFile(mFilter, Application.persistentDataPath, "generated_mesh");


            if (subdivide_mesh)
                MeshSmoothing.Subdivide(mFilter.mesh);
            //Debug.Log("vertex cnt2: " + mFilter.mesh.vertexCount);
            if (create_texture_atlas)
                mRender.material = texture_mat;
            else
                mRender.material = visible;

            Mesh mesh_ = mFilter.mesh;

            var uvs = new Vector2?[mesh_.vertices.Length];
            var tris = mesh_.triangles;

            List<int[]> triangle_list = new List<int[]>();
            for (idx = 0; idx < tris.Length; idx += 3)
            {
                int[] triangle = new int[3];
                triangle[0] = tris[idx]; triangle[1] = tris[idx + 1]; triangle[2] = tris[idx + 2];
                triangle_list.Add(triangle);
            }
            //Debug.Log("tri: " + mesh_.triangles[0] + "," + mesh_.triangles[1] + "," + mesh_.triangles[2]);
            //Debug.Log("tri: " + mesh_.triangles[1] + "," + mesh_.triangles[2] + "," + mesh_.triangles[3]);
            //break;

            for (idx = 0; idx < cam_poses_trans.Count; idx++)
            {
                cam.transform.position = cam_poses_trans[idx];
                cam.transform.rotation = cam_poses_rot[idx];

                //yield return new WaitForEndOfFrame();
                //Debug.Log("tris left: " + triangle_list.Count);
                List<int> remove_list = new List<int>();
                for (int tir_index = 0; tir_index < triangle_list.Count; tir_index++)
                {
                    var triangle = triangle_list[tir_index];
                    Vector3[] vertex = new Vector3[3];
                    Vector2?[] new_uvs = new Vector2?[3];
                    bool all_good = true;
                    for (int j = 0; j < 3; j++)
                    {
                        vertex[j] = mesh_.vertices[triangle[j]];

                        Vector2? new_uv = GetScreenPositionFromWorld(vertex[j], depth_textures[idx], cam);

                        if (new_uv != null && new_uv.HasValue)
                        {
                            new_uvs[j] = new_uv;
                        }
                        else
                        {
                            all_good = false;
                        }

                    }

                    if (all_good)
                    {
                        int cnt_used = 0;
                        for (int j = 0; j < 3; j++)
                        {
                            if (!uvs[triangle[j]].HasValue)
                            {
                                uvs[triangle[j]] = new Vector2(atlas_rec[idx].x, atlas_rec[idx].y) + new_uvs[j].Value * new Vector2(atlas_rec[idx].width, atlas_rec[idx].height);
                            }
                            else
                            {
                                cnt_used++;
                            }

                        }

                        if (cnt_used == 3)
                            Debug.Log("all already used");

                        remove_list.Add(tir_index);
                    }
                }

                foreach (int indice in remove_list.OrderByDescending(v => v))
                {
                    triangle_list.RemoveAt(indice);
                }

                //todo back down
                //yield return new WaitForEndOfFrame();                 
            }

            int cntn = 0;
            Vector2[] mesh_uvs = new Vector2[uvs.Length];
            for (int idx_ = 0; idx_ < mesh_uvs.Length; idx_++)
            {
                if (uvs[idx_].HasValue)
                {
                    mesh_uvs[idx_] = uvs[idx_].Value;
                    cntn++;
                }
                else
                    mesh_uvs[idx_] = new Vector2(0, 0); //todo
            }

            mesh_.uv = mesh_uvs;
            curr_meshes.Add(newMesh);
            Destroy(cam);
            Destroy(camObj);
            mesh_manager.DestroyAllMeshes();
            cam_poses_rot.Clear();
            cam_poses_trans.Clear();
            cam_textures.Clear();
            mesh_manager.enabled = false;        

        }
        else
        {
            btn_text.text = "show mesh";
            _cameraImageReceiver.enableOcclution(!showing);
            foreach (var obj in curr_meshes)
            {

                MeshFilter mFilter = obj.GetComponent<MeshFilter>();
                Destroy(obj);

            }

            mesh_manager.enabled = true;
            
        }
    }

    public void AddImages(Vector3 pose_t, Quaternion pose_q, Texture2D camTexture, Texture2D depthTexture)
    {
        Texture2D copyTexture = new Texture2D(camTexture.width, camTexture.height);
        copyTexture.SetPixels(camTexture.GetPixels());
        copyTexture.Apply();
        cam_textures.Add(copyTexture);

        copyTexture = new Texture2D(depthTexture.width, depthTexture.height, depthTexture.format, false);
        copyTexture.SetPixels(depthTexture.GetPixels());
        copyTexture.Apply();
        depth_textures.Add(copyTexture);

        cam_poses_trans.Add(pose_t);
        cam_poses_rot.Add(pose_q);
        
    }

    public void rayShoot()
    {
        move = !move;
        btn_ray_txt.text = move ? "fix" : "move";        

        if(move == false)
        {
            Vector2 uv = hitData2.textureCoord;
            MeshRenderer mr = hitData2.collider.GetComponent<MeshRenderer>();

            Texture2D tex = mr.material.mainTexture as Texture2D;
            uv.x *= tex.width;
            uv.y *= tex.height;

            Color c = tex.GetPixel((int)uv.x, (int)uv.y);
            Debug.Log(c);
        }
    }
}