using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.UI;
using System.IO;

public class TileManager:MonoBehaviour {

	public class Tile {
		public int value;
		public Space space;
		public GameObject tileOBJ;
		public GameObject valueOBJ;

		public Tile(int value,Space space) {
			this.value = value;
			this.space = space;

			tileOBJ = Instantiate(Resources.Load<GameObject>(@"Prefabs/space"),this.space.position,Quaternion.identity);
			tileOBJ.GetComponent<SpriteRenderer>().color = new Color(Mathf.Log(value,2),Mathf.Log(value,2),-Mathf.Log(value,2),255f) / 20f;
			tileOBJ.GetComponent<SpriteRenderer>().sortingOrder = 1;
			tileOBJ.transform.localScale = Vector2.one * 0.85f;
			tileOBJ.name = "Tile (" + this.space.position.x + "," + this.space.position.y + ")";

			valueOBJ = Instantiate(Resources.Load<GameObject>(@"Prefabs/value"),tileOBJ.transform.position,Quaternion.identity);
			valueOBJ.transform.SetParent(tileOBJ.transform);
			valueOBJ.transform.localScale = Vector2.one * 0.65f;
			valueOBJ.transform.Find("Text").GetComponent<Text>().text = this.value + "";
		}

		public void DestroyOBJs() {
			Destroy(tileOBJ);
			Destroy(valueOBJ);
		}

		public Vector2 source;
		public Vector2 target;
		public bool move;
		public float moveTimer;

		public void MoveToSpace(Space newSpace) {

			source = space.spaceOBJ.transform.position;
			target = newSpace.spaceOBJ.transform.position;

			move = true;
			moveTimer = 0;

			space.tile = null;
			space = newSpace;
			newSpace.tile = this;
		}

		public void MoveOBJ(float time) {
			tileOBJ.transform.position = Vector2.Lerp(source,target,moveTimer);
			valueOBJ.transform.position = tileOBJ.transform.position;
			if (moveTimer >= 1f) {
				move = false;
				tileOBJ.transform.position = target;
				valueOBJ.transform.position = target;
				return;
			}
			moveTimer += 7.5f * Time.deltaTime;
		}
	}

	public class Space {

		public Vector2 position;
		public GameObject spaceOBJ;

		public List<Space> surroundingSpaces = new List<Space>();

		public Tile tile;

		public TileManager tmRef;

		public Space(Vector2 position,TileManager tmRef) {
			this.position = position;

			spaceOBJ = Instantiate(Resources.Load<GameObject>(@"Prefabs/space"),this.position,Quaternion.identity);
			spaceOBJ.GetComponent<SpriteRenderer>().color = new Color(50f,50f,50f,255f) / 255f;
			spaceOBJ.name = "Space (" + this.position.x + "," + this.position.y + ")";

			this.tmRef = tmRef;

		}

		public void MergeTiles(Tile firstTile,Tile secondTile) {
			firstTile.DestroyOBJs();
			secondTile.DestroyOBJs();

			secondTile.space.tile = null;
			firstTile.space.tile = null;

			Tile mergedTile = new Tile(firstTile.value * 2,this);
			tile = mergedTile;

			tmRef.score += tile.value;
		}

		public float GetValue() {
			if (tile != null) {
				return tile.value;
			} else {
				return 0;
			}
		}
	}

	public List<Space> spaces = new List<Space>();
	public List<List<Space>> sortedSpaces = new List<List<Space>>();

	private int boardSize = 4;
	private int startTiles = 2;

	public void Awake() {
		StartGame();
	}

	public NeuralNetwork nn;

	void StartGame() {

		GameObject.Find("Main Camera").transform.position = new Vector2(boardSize / 2f - 0.5f,boardSize / 2f - 0.5f);
		GameObject.Find("Main Camera").GetComponent<Camera>().orthographicSize = boardSize / 2f + 1;

		for (int x = 0;x < boardSize;x++) {
			List<Space> innerSpaces = new List<Space>();
			for (int y = 0;y < boardSize;y++) {
				Space newSpace = new Space(new Vector2(y,x),this);
				spaces.Add(newSpace);
				innerSpaces.Add(newSpace);
			}
			sortedSpaces.Add(innerSpaces);
		}

		nn = new NeuralNetwork(this, spaces);

		for (int i = 0;i < spaces.Count;i++) {
			Space space = spaces[i];
			Vector2 gridIndex = new Vector2(i % boardSize,Mathf.FloorToInt(i / boardSize));
			if (gridIndex.y + 1 < boardSize) {
				space.surroundingSpaces.Add(sortedSpaces[(int)gridIndex.y + 1][(int)gridIndex.x]);
			} else {
				space.surroundingSpaces.Add(null);
			}
			if (gridIndex.x + 1 < boardSize) {
				space.surroundingSpaces.Add(sortedSpaces[(int)gridIndex.y][(int)gridIndex.x + 1]);
			} else {
				space.surroundingSpaces.Add(null);
			}
			if (gridIndex.y - 1 > -1) {
				space.surroundingSpaces.Add(sortedSpaces[(int)gridIndex.y - 1][(int)gridIndex.x]);
			} else {
				space.surroundingSpaces.Add(null);
			}
			if (gridIndex.x - 1 > -1) {
				space.surroundingSpaces.Add(sortedSpaces[(int)gridIndex.y][(int)gridIndex.x - 1]);
			} else {
				space.surroundingSpaces.Add(null);
			}
		}

		AddTiles(startTiles);
	}

	public void AddTiles(int num) {
		for (int i = 0;i < num;i++) {
			List<Space> openSpaces = new List<Space>();
			foreach (Space space in spaces) {
				if (space.tile == null) {
					openSpaces.Add(space);
				}
			}
			Space chosenSpace = openSpaces[Random.Range(0,openSpaces.Count)];
			Tile newTile = new Tile((Random.Range(0f,1f) < 0.9f ? 2 : 4),chosenSpace);
			chosenSpace.tile = newTile;
		}
	}

	public bool ShiftBoard(int direction) {

		List<List<Space>> usedList = sortedSpaces;

		for (int i = 0;i < direction+2;i++) {
			usedList = RotateBoard90(usedList);
		}
		bool moved = false;
		foreach(List<Space> row in usedList) {
			foreach (Space space in row) {
				if(space.tile != null) {
					Space currentSpace = space;
					while(currentSpace.surroundingSpaces[direction] != null && currentSpace.surroundingSpaces[direction].tile == null) {
						moved = true;
						currentSpace = currentSpace.surroundingSpaces[direction];
					}
					bool merged = false;
					if (currentSpace.surroundingSpaces[direction] != null && currentSpace.surroundingSpaces[direction].tile != null) {
						if (currentSpace.surroundingSpaces[direction].tile.value == space.tile.value) {
							currentSpace.surroundingSpaces[direction].MergeTiles(space.tile,currentSpace.surroundingSpaces[direction].tile);
							merged = true;
						}
					}
					if (merged) {
						moved = true;
						currentSpace.surroundingSpaces[direction].tile.MoveToSpace(currentSpace.surroundingSpaces[direction]);
					} else {
						space.tile.MoveToSpace(currentSpace);
					}
				}
			}
		}
		if (moved) {
			AddTiles(1);
		}

		return CheckGameOver();
	}

	public bool CheckGameOver() {
		foreach (Space space in spaces) {
			if (space.tile != null) {
				foreach (Space nSpace in space.surroundingSpaces) {
					if (nSpace != null) {
						if (nSpace.tile == null) {
							return false;
						} else if (nSpace.tile != null && nSpace.tile.value == space.tile.value) {
							return false;
						}
					}
				}
			} else {
				return false;
			}
		}
		return true;
	}

	public List<List<Space>> RotateBoard90(List<List<Space>> oldList) {
		List<List<Space>> newList = new List<List<Space>>();
		for (int x = 0;x < boardSize;x++) {
			List<Space> column = new List<Space>();
			for (int y = 0;y < boardSize;y++) {
				column.Add(oldList[y][x]);
			}
			column.Reverse();
			newList.Add(column);
		}
		return newList;
	}

	private int score;
	bool gameOver = false;
	bool end = false;
	private float runTimer = 0;

	public void Update() {

		if (runTimer > 1) {
			if (!gameOver && !end) {
				nn.Run();
			}
			runTimer = 0;
		} else {
			runTimer += 1000 * Time.deltaTime;
		}

		if (Input.GetKey(KeyCode.C)) {
			gameOver = ShiftBoard(UnityEngine.Random.Range(1,4));
		}
		
		if(Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow)) {
			gameOver = ShiftBoard(0);
		}
		if(Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow)) {
			gameOver = ShiftBoard(1);
		}
		if(Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow)) {
			gameOver = ShiftBoard(2);
		}
		if(Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow)) {
			gameOver = ShiftBoard(3);
		}

		foreach (Space space in spaces) {
			if (space.tile != null && space.tile.move) {
				space.tile.MoveOBJ(1f);
			}
		}

		GameObject.Find("ScoreText").GetComponent<Text>().text = score.ToString() + "\nIteration " + nn.networkStateIteration + " - " + nn.networkStateIndex + "\nBest " + (nn.previousBestNetworkState != null ? nn.previousBestNetworkState.score : 0);

		if (gameOver) {
			//if (Input.GetKeyDown(KeyCode.R)) {
			//print("Game over!");
			foreach (Space space in spaces) {
				if (space.tile != null) {
					space.tile.DestroyOBJs();
					space.tile = null;
				}
			}
			gameOver = false;
			score = 0;
			AddTiles(startTiles);
			//}
		}
	}

	float moveTimer = 0;
	public void AI() {
		float secondsBetweenMoves = 0.01f;
		secondsBetweenMoves = (secondsBetweenMoves <= 0.01f ? 0.01f : secondsBetweenMoves);
		moveTimer += (1f / secondsBetweenMoves) * Time.deltaTime;
		if (moveTimer > 1) {
			int shiftDirection = ShiftValueAnalysis();
			if (shiftDirection >= 0 && shiftDirection <= 3) {
				gameOver = ShiftBoard(shiftDirection);
			}
			moveTimer = 0;
		}
	}

	public List<bool> validDirections = new List<bool>();

	public int ShiftValueAnalysis() {
		validDirections.Clear();

		int highestDirectionValue = 0;
		int highestDirection = -1;
		int mostTileCombinations = 0;
		int mostTileCombinationDirection = -1;

		

		for (int i = 0;i < 4;i++) {
			int directionValue = 0;
			int numTileCombinations = 0;
			bool validDirection = false;
			foreach (Space space in spaces) {
				if (space.tile != null) {
					Space nSpace = space.surroundingSpaces[i];
					if (nSpace != null && ((nSpace.tile != null && nSpace.tile.value == space.tile.value) || (nSpace.tile == null))) {
						validDirection = true;
						if (highestDirection == -1) {
							highestDirection = i;
							mostTileCombinationDirection = i;
						}
					}
				}
			}
			validDirections.Add(validDirection);
			if (validDirection) {
				foreach (Space space in spaces) {
					if (space.tile != null) {
						foreach (Space nSpace in space.surroundingSpaces) {
							if (nSpace != null && nSpace.tile != null && nSpace.tile.value == space.tile.value) {
								directionValue += space.tile.value * 2;
								numTileCombinations += 1;
							}
						}
					}
				}
			}
			if (directionValue > highestDirectionValue) {
				highestDirection = i;
				highestDirectionValue = directionValue;
			}
			if (numTileCombinations > mostTileCombinations) {
				mostTileCombinationDirection = i;
				mostTileCombinations = numTileCombinations;
			}

			/*
			List<float> directionModifiers = new List<float>() { 0f,0f,0f,0f };

			if (validDirection) {
				foreach (Space space in spaces) {
					if (space.tile != null) {
						foreach (Space checkSpace in spaces) {
							if (checkSpace.tile != null && space.tile.value == checkSpace.tile.value) {
								Vector2 distance = new Vector2(0,0);
								distance.x = (space.tile.tileOBJ.transform.position.x - checkSpace.tile.tileOBJ.transform.position.x) * space.tile.value;
								distance.y = (space.tile.tileOBJ.transform.position.y - checkSpace.tile.tileOBJ.transform.position.y) * space.tile.value;
								directionModifiers[Mathf.RoundToInt
								directionModifiers[Mathf.RoundToInt(distance.y / Mathf.Abs(distance.y)) + 3 - 1 * ] = distance.y;
							}
						}
					}
				}
			}
			*/
		}

		/*
		Vector2 tileWeightCenter = new Vector2(0,0);
		float horizontalValueSum = 0;
		float horizontalTotalValues = 0;
		float verticalValueSum = 0;
		float verticalTotalValues = 0;
		foreach (Space space in spaces) {
			if (space.tile != null) {
				horizontalValueSum += (space.tile.tileOBJ.transform.position.x - 1.5f) * space.tile.value;
				horizontalTotalValues += 1;
				verticalValueSum += (space.tile.tileOBJ.transform.position.y - 1.5f) * space.tile.value;
				verticalTotalValues += 1;
			}
		}
		tileWeightCenter = new Vector2(Mathf.RoundToInt(horizontalValueSum / horizontalTotalValues),Mathf.RoundToInt(verticalValueSum / verticalTotalValues));
		print(highestDirection + " (" + highestDirectionValue + ") - " + mostTileCombinationDirection + " (" + mostTileCombinations + ")");
		*/

		highestDirection = mostTileCombinationDirection;

		/*
		GameObject.Find("CenterPositionVector").GetComponent<Text>().text = (tileWeightCenter.x) + "," + (tileWeightCenter.y);
		GameObject.Find("WeightPoint").GetComponent<RectTransform>().localPosition = new Vector2(tileWeightCenter.x+100,tileWeightCenter.y+100);

		validDirections.Clear();
		*/
		
		return highestDirection;
	}

	public class NeuralNetwork {

		public TileManager tileM;

		public List<List<Node>> nodes = new List<List<Node>>();

		public List<int> nodesPerLayer = new List<int>() {
			16,
			8,
			//8,
			//8,
			4
		};

		Dictionary<int, float> outputValues = new Dictionary<int, float>();

		public NeuralNetwork(TileManager tileM, List<Space> spaces) {
			this.tileM = tileM;
			for (int i = 0; i < nodesPerLayer.Count; i++) {
				nodes.Add(new List<Node>());
				for (int n = 0; n < nodesPerLayer[i]; n++) {
					NodeTypes nodeType = (i == 0 ? NodeTypes.Input : (i == nodesPerLayer.Count - 1 ? NodeTypes.Output : NodeTypes.Hidden));
					nodes[i].Add(new Node(this,nodeType,n,(i == 0 ? spaces[n] : null)));
				}
			}
			ConnectNodes();
			CreateVisual();
		}

		public void ConnectNodes() {
			int layerIndex = 0;
			foreach (List<Node> layer in nodes) {
				connectionsByLayer.Add(layerIndex, new List<Connection>());
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
			UpdateVisual();
		}

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
				if (invalidMoveCount > /*10*/0) {
					tileM.gameOver = true;
					invalidMoveCount = 0;
					tileM.end = !AddNetworkState();
				}
			} else {
				invalidMoveCount = 0;
			}
			outputValues.Clear();
		}

		public enum NodeTypes { Input,Hidden,Output };

		public class Node {

			public NeuralNetwork nn;

			public NodeTypes nodeType;
			public Space connectedSpace = null;

			public int layerIndex;

			public float value = 0;

			public List<Connection> incomingConnections = new List<Connection>();
			public List<Connection> outgoingConnections = new List<Connection>();

			public UINode uiNode;

			public Node(NeuralNetwork nn, NodeTypes nodeType, int layerIndex, Space connectedSpace) {
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
					value += connection.originNode.value * connection.weight;
				}
				if (squash) {
					value = SquashValue(value);
				}
			}
		}

		public Dictionary<int, List<Connection>> connectionsByLayer = new Dictionary<int, List<Connection>>();

		public class Connection {
			public Node originNode;
			public Node destinationNode;

			public float weight;

			public GameObject uiConnectionObj;

			public Connection(Node originNode, Node destinationNode) {
				this.originNode = originNode;
				this.destinationNode = destinationNode;
				weight = Random.Range(-1f, 1f);
			}
		}

		private int totalIterations = 100;
		private int networkStatesPerIteration = 100;
		public int networkStateIteration = 0;
		public int networkStateIndex = 0;

		public NetworkState previousBestNetworkState = null;

		public Dictionary<int, List<NetworkState>> networkStateIterations = new Dictionary<int, List<NetworkState>>();

		public bool AddNetworkState() {
			if (networkStateIteration >= totalIterations) {
				return false;
			}
			if (networkStateIterations.ContainsKey(networkStateIteration)) {
				networkStateIterations[networkStateIteration].Add(new NetworkState(this, tileM.score));
			} else {
				networkStateIterations.Add(networkStateIteration, new List<NetworkState>() { new NetworkState(this, tileM.score) });
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
				foreach (KeyValuePair<int, List<Connection>> connectionsByLayerKVP in connectionsByLayer) {
					int connectionIndex = 0;
					stateString += "\n" + connectionsByLayerKVP.Key;
					foreach (Connection connection in connectionsByLayerKVP.Value) {
						connection.weight = bestScoreNetworkState.connectionWeights[connectionsByLayerKVP.Key][connectionIndex];
						connectionIndex += 1;
						stateString += "`" + connectionIndex + ":" + connection.weight;
						/*
						if (save) {
							System.DateTime now = System.DateTime.Now;
							string dateTime = now.Year + "" + now.Month + "" + now.Day + "" + now.Hour + "" + now.Minute + "" + now.Second + "" + now.Millisecond;
							string fileName = Application.persistentDataPath + "/Data/data-" + networkStateIteration + "-" + networkStateIndex + "-" + dateTime + ".txt";
							FileStream settingsFile = new FileStream(fileName, FileMode.Create);
						}
						*/
					}
				}
				print(stateString);
				previousBestNetworkState = bestScoreNetworkState;
				networkStateIndex = 0;
				networkStateIteration += 1;
				networkStateIterations.Clear();
			} else {
				foreach (KeyValuePair<int, List<Connection>> connectionsByLayerKVP in connectionsByLayer) {
					int connectionIndex = 0;
					foreach (Connection connection in connectionsByLayerKVP.Value) {
						float changeValue = Random.Range(-1f, 1f) * (1 - (networkStateIteration / totalIterations));
						if (previousBestNetworkState != null) {
							connection.weight = previousBestNetworkState.connectionWeights[connectionsByLayerKVP.Key][connectionIndex] + changeValue;
						} else {
							connection.weight += changeValue;
							connection.weight = Node.SquashValue(connection.weight);
						}
						connectionIndex += 1;
					}
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
				foreach (KeyValuePair<int, List<Connection>> layerConnectionsKVP in nn.connectionsByLayer) {
					connectionWeights.Add(layerConnectionsKVP.Key, new List<float>());
					foreach (Connection connection in layerConnectionsKVP.Value) {
						connectionWeights[layerConnectionsKVP.Key].Add(connection.weight);
					}
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

		public class UILayer {
			public List<UINode> uiNodes = new List<UINode>();
			public GameObject obj;
			public GameObject connectionObj;
			public UILayer(List<Node> nodes, Transform parent, int layerIndex) {
				obj = Instantiate(Resources.Load<GameObject>(@"UI/UILayer"), parent, false);
				int nodeIndex = 0;
				foreach (Node node in nodes) {
					uiNodes.Add(new UINode(node, obj.transform, this, nodeIndex, layerIndex));
					nodeIndex += 1;
				}
			}
		}

		public class UINode {
			public Node node;
			public GameObject obj;
			public List<UIConnection> uiConnections = new List<UIConnection>();
			public UILayer uiLayer;
			public List<float> differenceVectorMagnitudes = new List<float>();
			public UINode(Node node,Transform parent,UILayer uiLayer, int nodeIndex, int layerIndex) {
				this.node = node;
				node.uiNode = this;
				this.uiLayer = uiLayer;
				obj = Instantiate(Resources.Load<GameObject>(@"UI/UINode"), GameObject.Find("NN-Panel").transform/*parent*/, false);
				obj.GetComponent<RectTransform>().anchoredPosition = new Vector2(layerIndex * (300f / node.nn.nodesPerLayer.Count) + ((300f / node.nn.nodesPerLayer.Count) / 2f), nodeIndex * (500f / node.nn.nodesPerLayer[layerIndex]) + ((500f / node.nn.nodesPerLayer[layerIndex]) / 2f));
				foreach (Connection connection in node.incomingConnections) {
					GameObject connectionObj = Instantiate(Resources.Load<GameObject>(@"UI/UIConnection"), GameObject.Find("Canvas").transform, false);
					RectTransform imageRectTransform = connectionObj.GetComponent<RectTransform>();

					Vector3 pointA = connection.originNode.uiNode.obj.transform.position;
					Vector3 pointB = obj.transform.position;

					Vector3 differenceVector = pointB - pointA;
					differenceVectorMagnitudes.Add(differenceVector.magnitude);

					imageRectTransform.sizeDelta = new Vector2(differenceVector.magnitude, Mathf.Abs(connection.weight) * 5f);
					imageRectTransform.pivot = new Vector2(0, 0.5f);
					imageRectTransform.position = pointA;

					float angle = Mathf.Atan2(differenceVector.y, differenceVector.x) * Mathf.Rad2Deg;
					imageRectTransform.rotation = Quaternion.Euler(0, 0, angle);

					connection.uiConnectionObj = connectionObj;
					uiConnections.Add(new UIConnection(connection,connectionObj));
				}
			}
		}

		public class UIConnection {
			public Connection connection;
			public GameObject obj;
			public UIConnection(Connection connection, GameObject obj) {
				this.connection = connection;
				this.obj = obj;
			}
		}

		public List<UILayer> uiNN = new List<UILayer>();

		public void CreateVisual() {
			int layerIndex = 0;
			foreach (List<Node> layer in nodes) {
				uiNN.Add(new UILayer(layer, GameObject.Find("NN-Panel").transform,layerIndex));
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
					uiNode.obj.transform.Find("Value").GetComponent<Text>().text = System.Math.Round(uiNode.node.value,2).ToString();
					int connectionIndex = 0;
					foreach (UIConnection uiConnection in uiNode.uiConnections) {
						uiConnection.obj.GetComponent<Image>().color = Color.Lerp(Color.red, Color.green, (uiConnection.connection.weight / 2f) + 0.5f);
						uiConnection.obj.GetComponent<RectTransform>().sizeDelta = new Vector2(uiNode.differenceVectorMagnitudes[connectionIndex], Mathf.Abs(uiNode.node.incomingConnections[connectionIndex].weight) * 5f);
						uiConnection.obj.transform.Find("Value").GetComponent<Text>().text = System.Math.Round(uiConnection.connection.weight,2).ToString();
						connectionIndex += 1;
					}
				}
			}
		}
	}
}
