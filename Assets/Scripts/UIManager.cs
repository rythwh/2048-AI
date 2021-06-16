using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour {

	private TileManager tileM;
	private NNManager nnM;

	void Awake() {
		tileM = GetComponent<TileManager>();
		nnM = GetComponent<NNManager>();
	}

	public class UILayer {
		public List<UINode> uiNodes = new List<UINode>();
		public GameObject obj;
		public GameObject connectionObj;
		public UILayer(List<NNManager.NeuralNetwork.Node> nodes, Transform parent, int layerIndex) {
			obj = Instantiate(Resources.Load<GameObject>(@"UI/UILayer"), parent, false);
			int nodeIndex = 0;
			foreach (NNManager.NeuralNetwork.Node node in nodes) {
				uiNodes.Add(new UINode(node, obj.transform, this, nodeIndex, layerIndex));
				nodeIndex += 1;
			}
		}
	}

	public class UINode {
		public NNManager.NeuralNetwork.Node node;
		public GameObject obj;
		public List<UIConnection> uiConnections = new List<UIConnection>();
		public UILayer uiLayer;
		public List<float> differenceVectorMagnitudes = new List<float>();
		public UINode(NNManager.NeuralNetwork.Node node, Transform parent, UILayer uiLayer, int nodeIndex, int layerIndex) {
			this.node = node;
			node.uiNode = this;
			this.uiLayer = uiLayer;
			obj = Instantiate(Resources.Load<GameObject>(@"UI/UINode"), GameObject.Find("NN-Panel").transform/*parent*/, false);
			obj.GetComponent<RectTransform>().anchoredPosition = new Vector2(layerIndex * (300f / node.nn.nodesPerLayer.Count) + ((300f / node.nn.nodesPerLayer.Count) / 2f), nodeIndex * (500f / node.nn.nodesPerLayer[layerIndex]) + ((500f / node.nn.nodesPerLayer[layerIndex]) / 2f));
			foreach (NNManager.NeuralNetwork.Connection connection in node.incomingConnections) {
				GameObject connectionObj = Instantiate(Resources.Load<GameObject>(@"UI/UIConnection"), GameObject.Find("NN-Panel").transform, false);
				RectTransform imageRectTransform = connectionObj.GetComponent<RectTransform>();

				Vector3 pointA = connection.originNode.uiNode.obj.transform.position;
				Vector3 pointB = obj.transform.position;

				Vector3 differenceVector = pointB - pointA;
				differenceVectorMagnitudes.Add(differenceVector.magnitude);

				imageRectTransform.sizeDelta = new Vector2(differenceVector.magnitude, Mathf.Abs(connection.GetWeight()) * 5f);
				imageRectTransform.pivot = new Vector2(0, 0.5f);
				imageRectTransform.position = pointA;

				float angle = Mathf.Atan2(differenceVector.y, differenceVector.x) * Mathf.Rad2Deg;
				imageRectTransform.rotation = Quaternion.Euler(0, 0, angle);

				connection.uiConnectionObj = connectionObj;
				uiConnections.Add(new UIConnection(connection, connectionObj));
			}
		}
	}

	public class UIConnection {
		public NNManager.NeuralNetwork.Connection connection;
		public GameObject obj;
		public UIConnection(NNManager.NeuralNetwork.Connection connection, GameObject obj) {
			this.connection = connection;
			this.obj = obj;
		}
	}

	public List<UILayer> uiNN = new List<UILayer>();

	public void CreateVisual() {
		int layerIndex = 0;
		foreach (List<NNManager.NeuralNetwork.Node> layer in nnM.nn.nodes) {
			uiNN.Add(new UILayer(layer, GameObject.Find("NN-Panel").transform, layerIndex));
			layerIndex += 1;
		}
		foreach (UILayer uiLayer in uiNN) {
			foreach (UINode uiNode in uiLayer.uiNodes) {
				foreach (UIConnection uiConnection in uiNode.uiConnections) {
					uiConnection.obj.transform.SetSiblingIndex(0);
				}
			}
		}
	}

	public void UpdateVisual() {
		foreach (UILayer uiLayer in uiNN) {
			foreach (UINode uiNode in uiLayer.uiNodes) {
				uiNode.obj.GetComponent<Image>().color = Color.Lerp(Color.red, Color.green, (uiNode.node.value / 2f) + 0.5f);
				uiNode.obj.transform.Find("Value").GetComponent<Text>().text = System.Math.Round(uiNode.node.value, 2).ToString();
				int connectionIndex = 0;
				foreach (UIConnection uiConnection in uiNode.uiConnections) {
					uiConnection.obj.GetComponent<Image>().color = Color.Lerp(Color.red, Color.green, (uiConnection.connection.GetWeight() / 2f) + 0.5f);
					uiConnection.obj.GetComponent<RectTransform>().sizeDelta = new Vector2(uiNode.differenceVectorMagnitudes[connectionIndex], Mathf.Abs(uiNode.node.incomingConnections[connectionIndex].GetWeight()) * 5f);
					uiConnection.obj.transform.Find("Value").GetComponent<Text>().text = System.Math.Round(uiConnection.connection.GetWeight(), 2).ToString();
					connectionIndex += 1;
				}
			}
		}
	}

	public void UpdateScore() {
		GameObject.Find("Score-Text").GetComponent<Text>().text = "Score \t" + tileM.score + "\n" + "High Score \t" + tileM.highScore + "\n" + (tileM.nnEnable ? "AI - Neural Network Mode" : tileM.mtcEnable ? "AI - Maximum Tile Combinations Mode" : "Manual Play");
	}

	public void UpdateNNScore() {
		GameObject.Find("NNScore-Text").GetComponent<Text>().text = tileM.score.ToString() + "\nIteration " + nnM.nn.networkStateIteration + " - " + nnM.nn.networkStateIndex + "\nBest " + (nnM.nn.previousBestNetworkState != null ? nnM.nn.previousBestNetworkState.score : 0);
	}
}
