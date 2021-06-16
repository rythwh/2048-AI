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
			//12,
			32,
			//6,
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
							// If = 1, all nodes with a piece regardless of size will have a value of 1.
							// With the alternative, nodes will have a value relative to the current largest tile value.
							node.value = 1;// Mathf.Log(node.connectedSpace.GetValue(), 2) / largestTileValue;
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
				float h = 2; // Difference between lowest and highest (e.g. 2 = range of -1 to 1).
				return h * (Mathf.Pow(e, preSquashValue) / (Mathf.Pow(e, preSquashValue) + 1)) - (h / 2);
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
			// If we've reached the maximum iterations, stop.
			if (networkStateIteration >= totalIterations) {
				return false;
			}

			// Save the current network state.
			previousNetworkState = new NetworkState(this, tileM.score);

			// If a networkStateIterations group for this iteration already exists...
			if (networkStateIterations.Count == networkStateIteration+1) {
				// Add this network state to the group.
				networkStateIterations[networkStateIteration].Add(previousNetworkState);
			} else {
				// Otherwise, create a new group.
				networkStateIterations.Add(new List<NetworkState>() { previousNetworkState });
			}
			networkStateIndex += 1;
			// If the number of games in this iteration has reached the maximum...
			if (networkStateIndex >= networkStatesPerIteration) {

				// Find which state had the best end score.
				NetworkState bestScoreNetworkState = networkStateIterations[networkStateIteration].OrderByDescending(ns => ns.score).ToList()[0];

				//bool save = true;
				// If there is already a best previous state, compare this iteration's best state to the global best state and overwrite it if necessary.
				if (previousBestNetworkState != null && bestScoreNetworkState.score < previousBestNetworkState.score) {
					bestScoreNetworkState = previousBestNetworkState;
					//save = false;
				}


				string stateString = networkStateIteration.ToString() + " " + bestScoreNetworkState.score;

				// Iterate over each connection in each layer
				int connectionsByLayerIndex = 0;
				foreach (List<Connection> connectionsByLayer in connectionsByLayer) {
					int connectionIndex = 0;
					stateString += "\n" + connectionsByLayerIndex;
					foreach (Connection connection in connectionsByLayer) {
						// Set the connection's weight to what its weight was in the best network state
						//connection.SetWeight(bestScoreNetworkState.connectionWeights[connectionsByLayerIndex][connectionIndex]);
						
						stateString += "`" + connectionIndex + ":" + bestScoreNetworkState.connectionWeights[connectionsByLayerIndex][connectionIndex];//connection.GetWeight();
						connectionIndex += 1;

						// Save the best network state to a file if this iteration was the best globally
						/*if (save) {
							System.DateTime now = System.DateTime.Now;
							string dateTime = now.Year + "" + now.Month + "" + now.Day + "" + now.Hour + "" + now.Minute + "" + now.Second + "" + now.Millisecond;
							string fileName = Application.persistentDataPath + "/Data/data-" + networkStateIteration + "-" + networkStateIndex + "-" + dateTime + ".txt";
							FileStream settingsFile = new FileStream(fileName, FileMode.Create);
						}*/

					}
					connectionsByLayerIndex += 1;
				}
				print(stateString);
				previousBestNetworkState = bestScoreNetworkState;
				networkStateIndex = 0;
				networkStateIteration += 1;
				networkStateIterations.Clear();

				previousNetworkState = null;

			// If this game was not the last in the iteration...
			} else {

				// Iterate over each connection in each layer
				int connectionsByLayerIndex = 0;
				foreach (List<Connection> connectionsByLayer in connectionsByLayer) {
					int connectionIndex = 0;
					foreach (Connection connection in connectionsByLayer) {

						// Determine a value to randomly change the weight of this connection by
						float changeValue = Random.Range(-0.1f, 0.1f);// * (1 - (networkStateIteration / totalIterations));

						// If this is not the first iteration and not the first game in this iteration
						if (previousBestNetworkState != null && previousNetworkState != null) {

							// Compare the best network state score to the current state's score
							if (previousBestNetworkState.score > previousNetworkState.score) {
								// If the best network state is better than this state...
								//print(previousBestNetworkState.connectionWeights[connectionsByLayerIndex][connectionIndex] + " " + connection.GetWeight());
								//changeValue *= previousBestNetworkState.connectionWeights[connectionsByLayerIndex][connectionIndex] - connection.GetWeight();
							} else {
								// If this state is better than the best network state...
								//print(previousBestNetworkState.connectionWeights[connectionsByLayerIndex][connectionIndex] + " " + connection.GetWeight());
								//changeValue *= connection.GetWeight() - previousBestNetworkState.connectionWeights[connectionsByLayerIndex][connectionIndex];
							}
						}

						// If this is not the first iteration
						if (previousBestNetworkState != null) {
							//print(changeValue);
							// Set the connection's weight to the weight from the best network state plus the random change value.
							connection.SetWeight(previousBestNetworkState.connectionWeights[connectionsByLayerIndex][connectionIndex] + changeValue);
							//connection.SetWeight(Node.SquashValue(connection.GetWeight()));

						// If this is the first iteration
						} else {
							// Set the connection's weight to itself plus the random change value.
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
