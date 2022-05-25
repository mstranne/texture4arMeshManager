/*
 * @author mattatz / http://mattatz.github.io

 * https://www.researchgate.net/publication/220507688_Improved_Laplacian_Smoothing_of_Noisy_Surface_Meshes
 * http://graphics.stanford.edu/courses/cs468-12-spring/LectureSlides/06_smoothing.pdf
 * http://wiki.unity3d.com/index.php?title=MeshSmoother
 */

using UnityEngine;

using System;
using System.Collections;
using System.Collections.Generic;

public class MeshSmoothing
{

	public static Mesh LaplacianFilter(Mesh mesh, int times = 1)
	{
		mesh.vertices = LaplacianFilter(mesh.vertices, mesh.triangles, times);
		mesh.RecalculateNormals();
		mesh.RecalculateBounds();
		return mesh;
	}

	public static Vector3[] LaplacianFilter(Vector3[] vertices, int[] triangles, int times)
	{
		var network = VertexConnection.BuildNetwork(triangles);
		for (int i = 0; i < times; i++)
		{
			vertices = LaplacianFilter(network, vertices, triangles);
		}
		return vertices;
	}

	static Vector3[] LaplacianFilter(Dictionary<int, VertexConnection> network, Vector3[] origin, int[] triangles)
	{
		Vector3[] vertices = new Vector3[origin.Length];
		for (int i = 0, n = origin.Length; i < n; i++)
		{
			var connection = network[i].Connection;
			var v = Vector3.zero;
			foreach (int adj in connection)
			{
				v += origin[adj];
			}
			vertices[i] = v / connection.Count;
		}
		return vertices;
	}

	/*
		* HC (Humphrey’s Classes) Smooth Algorithm - Reduces Shrinkage of Laplacian Smoother
		* alpha 0.0 ~ 1.0
		* beta  0.0 ~ 1.0
	*/
	public static Mesh HCFilter(Mesh mesh, int times = 5, float alpha = 0.5f, float beta = 0.75f)
	{
		mesh.vertices = HCFilter(mesh.vertices, mesh.triangles, times, alpha, beta);
		mesh.RecalculateNormals();
		mesh.RecalculateBounds();
		return mesh;
	}

	static Vector3[] HCFilter(Vector3[] vertices, int[] triangles, int times, float alpha, float beta)
	{
		alpha = Mathf.Clamp01(alpha);
		beta = Mathf.Clamp01(beta);

		var network = VertexConnection.BuildNetwork(triangles);

		Vector3[] origin = new Vector3[vertices.Length];
		Array.Copy(vertices, origin, vertices.Length);
		for (int i = 0; i < times; i++)
		{
			vertices = HCFilter(network, origin, vertices, triangles, alpha, beta);
		}
		return vertices;
	}

	public static Vector3[] HCFilter(Dictionary<int, VertexConnection> network, Vector3[] o, Vector3[] q, int[] triangles, float alpha, float beta)
	{
		Vector3[] p = LaplacianFilter(network, q, triangles);
		Vector3[] b = new Vector3[o.Length];

		for (int i = 0; i < p.Length; i++)
		{
			b[i] = p[i] - (alpha * o[i] + (1f - alpha) * q[i]);
		}

		for (int i = 0; i < p.Length; i++)
		{
			var adjacents = network[i].Connection;
			var bs = Vector3.zero;
			foreach (int adj in adjacents)
			{
				bs += b[adj];
			}
			p[i] = p[i] - (beta * b[i] + (1 - beta) / adjacents.Count * bs);
		}

		return p;
	}

	static List<Vector3> vertices;

	static List<Vector3> normals;
	// [... all other vertex data arrays you need]

	static List<int> indices;
	static Dictionary<uint, int> newVectices;

	static int GetNewVertex(int i1, int i2)
	{
		// We have to test both directions since the edge
		// could be reversed in another triangle
		uint t1 = ((uint)i1 << 16) | (uint)i2;
		uint t2 = ((uint)i2 << 16) | (uint)i1;
		if (newVectices.ContainsKey(t2))
			return newVectices[t2];
		if (newVectices.ContainsKey(t1))
			return newVectices[t1];
		// generate vertex:
		int newIndex = vertices.Count;
		newVectices.Add(t1, newIndex);

		// calculate new vertex
		vertices.Add((vertices[i1] + vertices[i2]) * 0.5f);
		normals.Add((normals[i1] + normals[i2]).normalized);
		// [... all other vertex data arrays]

		return newIndex;
	}


	public static void Subdivide(Mesh mesh)
	{
		newVectices = new Dictionary<uint, int>();

		vertices = new List<Vector3>(mesh.vertices);
		normals = new List<Vector3>(mesh.normals);
		// [... all other vertex data arrays]
		indices = new List<int>();

		int[] triangles = mesh.triangles;
		for (int i = 0; i < triangles.Length; i += 3)
		{
			int i1 = triangles[i + 0];
			int i2 = triangles[i + 1];
			int i3 = triangles[i + 2];

			int a = GetNewVertex(i1, i2);
			int b = GetNewVertex(i2, i3);
			int c = GetNewVertex(i3, i1);
			indices.Add(i1);
			indices.Add(a);
			indices.Add(c);
			indices.Add(i2);
			indices.Add(b);
			indices.Add(a);
			indices.Add(i3);
			indices.Add(c);
			indices.Add(b);
			indices.Add(a);
			indices.Add(b);
			indices.Add(c); // center triangle
		}

		mesh.vertices = vertices.ToArray();
		mesh.normals = normals.ToArray();
		// [... all other vertex data arrays]
		mesh.triangles = indices.ToArray();

		// since this is a static function and it uses static variables
		// we should erase the arrays to free them:
		newVectices = null;
		vertices = null;
		normals = null;
		// [... all other vertex data arrays]

		indices = null;
	}
}


