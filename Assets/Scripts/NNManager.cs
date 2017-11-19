using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class NNManager : MonoBehaviour {

	public NeuralNetwork nn;

	public class NeuralNetwork {

		public TileManager tileM;
		private UIManager uiM;

		public List<List<Node>> nodes = new List<List<Node>>();

		public List<int> nodesPerLayer = new List<int>() {
			16,
			8,
			//8,
			//8,
			4
		};

		Dictionary<int, float> outputValues = new Dictionary<int, float>();

		public NeuralNetwork(TileManager tileM, List<TileManager.Space> spaces, UIManager uiM) {
			this.tileM = tileM;
			this.uiM = uiM;
			for (int i = 0; i < nodesPerLayer.Count; i++) {
				nodes.Add(new List<Node>());
				for (int n = 0; n < nodesPerLayer[i]; n++) {
					NodeTypes nodeType = (i == 0 ? NodeTypes.Input : (i == nodesPerLayer.Count - 1 ? NodeTypes.Output : NodeTypes.Hidden));
					nodes[i].Add(new Node(this, nodeType, n, (i == 0 ? spaces[n] : null)));
				}
			}
			ConnectNodes();
		}

		public void ConnectNodes() {
			int layerIndex = 0;
			foreach (List<Node> layer in nodes) {
				connectionsByLayer.Add(new List<Connection>());
				foreach (Node node in layer) {
					if (layerIndex < nodes.Count - 1) {
						foreach (Node outgoingNode in nodes[layerIndex + 1]) {
							Connection newConnection = new Connection(node, outgoingNode);
							node.outgoingConnections.Add(newConnection);
							outgoingNode.incomingConnections.Add(newConnection);
							connectionsByLayer[layerIndex].Add(newConnection);
						}
					}
				}
				layerIndex += 1;
			}
		}

		public void Run() {
			float largestTileValue = Mathf.Log(Mathf.RoundToInt(tileM.spaces.Max(space => space.GetValue())), 2);
			foreach (List<Node> layer in nodes) {
				int nodeIndex = 0;
				foreach (Node node in layer) {
					if (node.nodeType == NodeTypes.Input) {
						if (node.connectedSpace.GetValue() == 0) {
							node.value = 0;
						} else {
							node.value = Mathf.Log(node.connectedSpace.GetValue(), 2) / largestTileValue;
						}
					} else if (node.nodeType == NodeTypes.Output) {
						node.AddIncomingValues(false);
						outputValues.Add(nodeIndex, node.value);
					} else {
						node.AddIncomingValues(true);
					}
					nodeIndex += 1;
				}
			}
			tileM.ShiftValueAnalysis();
			ChooseMove();
			uiM.UpdateVisual();
		}

		private int invalidMoveCountMax = 0; // 10;
		private int invalidMoveCount = 0;

		public void ChooseMove() {
			KeyValuePair<int, float> highestKVP = new KeyValuePair<int, float>(0, outputValues[0]);
			foreach (KeyValuePair<int, float> outputNodeKVP in outputValues) {
				if (outputNodeKVP.Value > highestKVP.Value) {
					highestKVP = outputNodeKVP;
				}
			}
			tileM.ShiftBoard(highestKVP.Key);
			if (!tileM.validDirections[highestKVP.Key]) {
				invalidMoveCount += 1;
				if (invalidMoveCount > invalidMoveCountMax) {
					tileM.gameOver = true;
					invalidMoveCount = 0;
					tileM.end = !AddNetworkState();
				}
			} else {
				invalidMoveCount = 0;
			}
			outputValues.Clear();
		}

		public enum NodeTypes { Input, Hidden, Output };

		public class Node {

			public NeuralNetwork nn;

			public NodeTypes nodeType;
			public TileManager.Space connectedSpace = null;

			public int layerIndex;

			public float value = 0;

			public List<Connection> incomingConnections = new List<Connection>();
			public List<Connection> outgoingConnections = new List<Connection>();

			public UIManager.UINode uiNode;

			public Node(NeuralNetwork nn, NodeTypes nodeType, int layerIndex, TileManager.Space connectedSpace) {
				this.nn = nn;
				this.nodeType = nodeType;
				this.layerIndex = layerIndex;
				if (nodeType == NodeTypes.Input) {
					this.connectedSpace = connectedSpace;
				}
			}

			public static float SquashValue(float preSquashValue) {
				float e = 2.71828f;
				return (((Mathf.Pow(e, preSquashValue)) / (Mathf.Pow(e, preSquashValue) + 1)) - 0.5f);
			}

			public void AddIncomingValues(bool squash) {
				value = 0;
				foreach (Connection connection in incomingConnections) {
					value += connection.originNode.value * connection.GetWeight();
				}
				if (squash) {
					value = SquashValue(value);
				}
			}
		}

		public List<List<Connection>> connectionsByLayer = new List<List<Connection>>();

		public class Connection {
			public Node originNode;
			public Node destinationNode;

			private float prevWeight;
			private float weight;

			public GameObject uiConnectionObj;

			public Connection(Node originNode, Node destinationNode) {
				this.originNode = originNode;
				this.destinationNode = destinationNode;
				weight = Random.Range(-1f, 1f);
			}

			public void SetWeight(float weight) {
				prevWeight = this.weight;
				this.weight = weight;
			}

			public float GetWeight() {
				return weight;
			}

			public float GetPreviousWeight() {
				return prevWeight;
			}
		}

		private int totalIterations = 100;
		private int networkStatesPerIteration = 100;
		public int networkStateIteration = 0;
		public int networkStateIndex = 0;

		public NetworkState previousBestNetworkState = null;

		public List<List<NetworkState>> networkStateIterations = new List<List<NetworkState>>();

		public NetworkState previousNetworkState = null;

		public bool AddNetworkState() {
			if (networkStateIteration >= totalIterations) {
				return false;
			}
			previousNetworkState = new NetworkState(this, tileM.score);
			if (networkStateIterations.Count == networkStateIteration+1) {
				networkStateIterations[networkStateIteration].Add(previousNetworkState);
			} else {
				networkStateIterations.Add(new List<NetworkState>() { previousNetworkState });
			}
			networkStateIndex += 1;
			if (networkStateIndex >= networkStatesPerIteration) {
				NetworkState bestScoreNetworkState = networkStateIterations[networkStateIteration].OrderByDescending(ns => ns.score).ToList()[0];
				//bool save = true;
				if (previousBestNetworkState != null && bestScoreNetworkState.score < previousBestNetworkState.score) {
					bestScoreNetworkState = previousBestNetworkState;
					//save = false;
				}
				string stateString = networkStateIteration.ToString() + " " + bestScoreNetworkState.score;
				int connectionsByLayerIndex = 0;
				foreach (List<Connection> connectionsByLayer in connectionsByLayer) {
					int connectionIndex = 0;
					stateString += "\n" + connectionsByLayerIndex;
					foreach (Connection connection in connectionsByLayer) {
						connection.SetWeight(bestScoreNetworkState.connectionWeights[connectionsByLayerIndex][connectionIndex]);
						connectionIndex += 1;
						stateString += "`" + connectionIndex + ":" + connection.GetWeight();
						/*
						if (save) {
							System.DateTime now = System.DateTime.Now;
							string dateTime = now.Year + "" + now.Month + "" + now.Day + "" + now.Hour + "" + now.Minute + "" + now.Second + "" + now.Millisecond;
							string fileName = Application.persistentDataPath + "/Data/data-" + networkStateIteration + "-" + networkStateIndex + "-" + dateTime + ".txt";
							FileStream settingsFile = new FileStream(fileName, FileMode.Create);
						}
						*/
					}
					connectionsByLayerIndex += 1;
				}
				print(stateString);
				previousBestNetworkState = bestScoreNetworkState;
				networkStateIndex = 0;
				networkStateIteration += 1;
				networkStateIterations.Clear();
			} else {
				int connectionsByLayerIndex = 0;
				foreach (List<Connection> connectionsByLayer in connectionsByLayer) {
					int connectionIndex = 0;
					foreach (Connection connection in connectionsByLayer) {
						float changeValue = Random.Range(-1f, 1f) * (1 - (networkStateIteration / totalIterations));
						if (previousBestNetworkState != null && previousNetworkState != null) {
							if (previousBestNetworkState.score > previousNetworkState.score) {
								changeValue *= connection.GetPreviousWeight() - connection.GetWeight();
							} else {
								changeValue *= connection.GetWeight() - connection.GetPreviousWeight();
							}
						}
						if (previousBestNetworkState != null) {
							connection.SetWeight(previousBestNetworkState.connectionWeights[connectionsByLayerIndex][connectionIndex] + changeValue);
						} else {
							connection.SetWeight(connection.GetWeight() + changeValue);
							connection.SetWeight(Node.SquashValue(connection.GetWeight()));
						}
						connectionIndex += 1;
					}
					connectionsByLayerIndex += 1;
				}
			}
			return true;
		}

		public class NetworkState {
			public NeuralNetwork nn;

			public int score;
			public Dictionary<int, List<float>> connectionWeights = new Dictionary<int, List<float>>();

			public NetworkState(NeuralNetwork nn, int score) {
				this.nn = nn;
				this.score = score;
				int layerConnectionIndex = 0;
				foreach (List<Connection> layerConnections in nn.connectionsByLayer) {
					connectionWeights.Add(layerConnectionIndex, new List<float>());
					foreach (Connection connection in layerConnections) {
						connectionWeights[layerConnectionIndex].Add(connection.GetWeight());
					}
					layerConnectionIndex += 1;
				}
			}

			public void AddConnectionWeight(int layerIndex, float weight) {
				if (connectionWeights.ContainsKey(layerIndex)) {
					connectionWeights[layerIndex].Add(weight);
				} else {
					connectionWeights.Add(layerIndex, new List<float>() { weight });
				}
			}
		}
	}
}
