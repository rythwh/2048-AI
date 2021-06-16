using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.UI;
using System.IO;

public class TileManager:MonoBehaviour {

	private UIManager uiM;
	private NNManager nnM;

	private StreamWriter outputFile;

	public void Awake() {
		uiM = GetComponent<UIManager>();
		nnM = GetComponent<NNManager>();

		System.DateTime now = System.DateTime.Now;
		string dateTime = now.Year + "y" + now.Month + "m" + now.Day + "d" + now.Hour + "h" + now.Minute + "m" + now.Second + "s" + now.Millisecond + "m";
		outputFile = new StreamWriter(Application.persistentDataPath + "/data_" + dateTime + ".txt");
	}

	public void Start() {
		StartGame();
	}

	public class Tile {
		public int value;
		public Space space;
		public GameObject tileOBJ;
		public GameObject valueOBJ;

		public Tile(int value,Space space) {
			this.value = value;
			this.space = space;

			tileOBJ = Instantiate(Resources.Load<GameObject>(@"Prefabs/space"),this.space.position,Quaternion.identity);
			tileOBJ.GetComponent<SpriteRenderer>().color = new Color(Mathf.Log(value,2),Mathf.Log(value,2),Mathf.Log(value,2),255f) / 20f;
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
			spaceOBJ.GetComponent<SpriteRenderer>().color = new Color(145f,111f,78f,255f) / 255f;
			spaceOBJ.transform.localScale = Vector2.one * 0.95f;
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

		nnM.nn = new NNManager.NeuralNetwork(this, spaces, uiM);
		uiM.CreateVisual();

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

		for (int i = 0; i < direction + 2; i++) {
			usedList = RotateBoard90(usedList);
		}
		bool moved = false;
		foreach (List<Space> row in usedList) {
			foreach (Space space in row) {
				if (space.tile != null) {
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

		uiM.UpdateScore();
		uiM.UpdateNNScore();

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

	public int score = 0;
	public int highScore = 0;
	public bool gameOver = false;
	public bool end = false;
	private float runTimer = 0;

	public bool nnEnable = false;
	public bool mtcEnable = false;

	public void Update() {

		if (Input.GetKeyDown(KeyCode.N)) {
			if (nnEnable) {
				outputFile.Close();
				end = true;
			}
			nnEnable = true;
			mtcEnable = false;
		} else if (Input.GetKeyDown(KeyCode.M)) {
			if (mtcEnable) {
				outputFile.Close();
				end = true;
			}
			nnEnable = false;
			mtcEnable = true;
		}
		if (runTimer > 1) {
			if (!gameOver && !end) {
				if (nnEnable) {
					nnM.nn.Run();
				} else if (mtcEnable) {
					MostTileCombinationsAI();
				}
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

		if (gameOver) {
			if (nnEnable || mtcEnable) {
				ResetBoard();
			} else {
				if (Input.GetKeyDown(KeyCode.R)) {
					//print("Game over!");
					ResetBoard();
				}
			}
		}
		if (end) {
			outputFile.Close();
		}
	}

	public void ResetBoard() {
		foreach (Space space in spaces) {
			if (space.tile != null) {
				space.tile.DestroyOBJs();
				space.tile = null;
			}
		}
		gameOver = false;
		//print(score);
		outputFile.WriteLine(score);
		if (score > highScore) {
			highScore = score;
		}
		score = 0;
		AddTiles(startTiles);
	}

	float moveTimer = 0;
	public void MostTileCombinationsAI() {
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

	
}
