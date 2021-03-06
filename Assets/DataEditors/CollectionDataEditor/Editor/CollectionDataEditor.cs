﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.IO;

	namespace DataEditor.CollectionData{
	public class DataEditor : EditorWindow, IHasCustomMenu {

		//When to save database:
		//	- When the editor is closed
		//	- When user select another entry
		//	- 2 seconds after user change a value of a entry

		//When to filter:
		//	- When filter field is changed, and the new value is not same or ""

		private static DataEditor _editor;
		public static DataEditor editor { get { AssureEditor(); return _editor; } }
		public static void AssureEditor() { if (_editor == null) OpenNodeEditor(); }

		public static string databaseDir = "Assets/DataEditors/CollectionDataEditor/Resources/";
		public static string databaseName = "CollectionDatabase";

		public static CollectionDatabase databaseCache;
		static CollectionDataEntry entryCache;

		static GUISkin itemEditorSkin;

		static bool CollectionDataEditorDebugging = false;

		static void SaveDatabase(){
			EditorUtility.SetDirty(databaseCache);
			AssetDatabase.SaveAssets();
			if (CollectionDataEditorDebugging) Debug.Log("CollectionDataEditor:: SaveDatabase():: Database saved.");
		}


		static void LoadDatabase(){
			
			if (CollectionDataEditorDebugging) Debug.Log("CollectionDataEditor:: LoadDatabase():: Start.");
			
			databaseCache = (CollectionDatabase) Resources.Load(databaseName, typeof(CollectionDatabase));
			if (!databaseCache) {
				Directory.CreateDirectory(databaseDir);
				databaseCache = CollectionDatabase.Create();
				AssetDatabase.CreateAsset(databaseCache, databaseDir + databaseName + ".asset");
				SaveDatabase();
				if (CollectionDataEditorDebugging) Debug.Log("CollectionDataEditor:: LoadDatabase():: Created a new database asset because no database asset found at designated path.");
			}
			
			if (CollectionDataEditorDebugging) Debug.Log("CollectionDataEditor:: LoadDatabase():: Done.");
		}  

		///Load entry content into Editor block, does not thing to do with (int) selected.
		static void LoadEntry(CollectionDataEntry entry){
			if (CollectionDataEditorDebugging) Debug.Log("CollectionDataEditor:: LoadEntry():: Loading " + entry.itemID);
			entryCache = entry;
		}

		public static void AddSubAsset(ScriptableObject subAsset, ScriptableObject mainAsset)
		{
			if (subAsset != null && mainAsset != null)
			{
				AssetDatabase.AddObjectToAsset(subAsset, mainAsset);
				subAsset.hideFlags = HideFlags.HideInHierarchy;
			}
		}

		static void DoBackup(){
			databaseCache.lastBackupTime = System.DateTime.Now.Ticks;
			if (File.Exists(databaseDir + databaseName + ".asset.backup")) AssetDatabase.DeleteAsset(databaseDir + databaseName + ".asset.backup");
			if (File.Exists(databaseDir + databaseName + ".asset")) AssetDatabase.CopyAsset(databaseDir + databaseName + ".asset", databaseDir + databaseName + ".asset.backup");
		}

		const int sidebarWidth = 200, viewInset = 4;

		Rect thumbnailRect { get { return new Rect(sidebarWidth + viewInset + 10, viewInset + 24, 300, 100); }}
		Rect imageRect { get { return new Rect(sidebarWidth + viewInset + 10, viewInset + 144, 300, 300); }}
		Rect SidebarRect { get { return new Rect(viewInset, viewInset, sidebarWidth, position.height - viewInset*2); }}
		Rect EditorRect { get { return new Rect (sidebarWidth + viewInset + 10, viewInset, position.width - sidebarWidth - viewInset*2, position.height - viewInset*2); }}

		Vector2 scrollpos;

		static Texture2D defaultImg, workingImg;

		static void CheckInit(){			
			if (!defaultImg) defaultImg = Resources.Load("DefaultTex", typeof(Texture2D)) as Texture2D;
			if (!workingImg) workingImg = Resources.Load("Hardworking", typeof(Texture2D)) as Texture2D;
			if (!databaseCache){
				if (CollectionDataEditorDebugging) Debug.Log("CollectionDataEditor:: CheckInit():: itemDatabaseCache is null.");
				LoadDatabase();
				filteredList = databaseCache.itemList;
			}			
			if (itemNamesCache == null) {
				if (CollectionDataEditorDebugging) Debug.Log("CollectionDataEditor:: CheckInit():: itemNamesCache is null.");
				CacheItemNames();
				filteredItemNamesCache = new List<string>(itemNamesCache);
			}
			if (!itemEditorSkin) itemEditorSkin = Resources.Load<GUISkin>("ItemEditor");

			if (dDrawSidebarContent == null) {
				if (CollectionDataEditorDebugging) Debug.Log("CollectionDataEditor:: CheckInit():: Assigning dDrawSidebarContent to DrawSelections.");
				dDrawSidebarContent = Sidebar_DrawSelections;
			}
			if (dDrawEditorContent == null) {
				if (CollectionDataEditorDebugging) Debug.Log("CollectionDataEditor:: CheckInit():: Assigning dDrawEditorContent to DrawEditor.");
				dDrawEditorContent = EditorContent_DrawEditor;
			}

			if (selected == -1){
				if (CollectionDataEditorDebugging) Debug.Log("CollectionDataEditor:: CheckInit():: selected == -1, set to 0.");
				selected = 0;
			}

			if (entryCache == null) {
				if (CollectionDataEditorDebugging) Debug.Log("CollectionDataEditor:: CheckInit():: entryCache is null. Trying to load filteredList[" + selected + "].");
				if (filteredList.Count > 0) {
					LoadEntry(filteredList[selected]);
					dDrawEditorContent = EditorContent_DrawEditor;
				}
				else {
					if (CollectionDataEditorDebugging) Debug.Log("CollectionDataEditor:: CheckInit():: filteredList is empty.");	
					dDrawEditorContent = EditorContent_NoSelection;
				}
			}
			
		}

        public void AddItemsToMenu(GenericMenu menu)
        {
            menu.AddItem(new GUIContent("Export To Json File"), false, new GenericMenu.MenuFunction(OnExportButtonClicked));
            menu.AddItem(new GUIContent("Import From Json File"), false, new GenericMenu.MenuFunction(OnImportButtonClicked));
        }


		[MenuItem ("Window/Data Editors/Collection Data")]
		public static DataEditor OpenNodeEditor () 
		{
			_editor = GetWindow<DataEditor>();
			_editor.minSize = new Vector2(800, 600);
			_editor.maxSize = new Vector2(1280, 720);

			_editor.titleContent = new GUIContent ("Item Editor");

			if (CollectionDataEditorDebugging) Debug.Log("CollectionDataEditor:: OpenNodeEditor():: Done.");
			return _editor;
		}

		/// <summary>
		/// OnGUI is called for rendering and handling GUI events.
		/// This function can be called multiple times per frame (one call per event).
		/// </summary>
		void OnGUI()
		{
			CheckInit();

			GUILayout.BeginHorizontal();
				GUILayout.Space(4);
				DrawSidebar();
				
				GUILayout.Space(10);

				DrawEditor();
				GUILayout.Space(20);
			GUILayout.EndHorizontal();

			if (selected != selectedOld) OnSidebarSelectionChanged();

			if (System.DateTime.Now.Ticks - databaseCache.lastBackupTime > 6000000000) DoBackup();
		}

#region EditorDrawing

		delegate void DrawEditorContent();
		static DrawEditorContent dDrawEditorContent;

		static void EditorContent_Filtering(){
			GUILayout.BeginVertical(GUILayout.ExpandHeight(true), GUILayout.ExpandWidth(true));
				ToggleBoxSkin(true);
				GUILayout.Box(workingImg, GUILayout.ExpandHeight(true), GUILayout.ExpandWidth(true));
				ToggleBoxSkin(false);
			GUILayout.EndVertical();
		}

		static void EditorContent_NoSelection(){
			GUILayout.BeginVertical(GUILayout.ExpandHeight(true), GUILayout.ExpandWidth(true));
				ToggleBoxSkin(true);
				GUILayout.Box(workingImg, GUILayout.ExpandHeight(true), GUILayout.ExpandWidth(true));
				ToggleBoxSkin(false);
			GUILayout.EndVertical();
		}

		static void EditorContent_DrawEditor(){
			editorScrollPos = GUILayout.BeginScrollView(editorScrollPos);
				GUILayout.BeginVertical();
					GUILayout.Space(4);
					GUILayout.Label("Content");
					GUILayout.BeginHorizontal(GUILayout.Height(140));
						GUILayout.BeginVertical(GUILayout.Width(120));
							GUILayout.FlexibleSpace();
							ToggleBoxSkin(true);
							if (!entryCache.itemThumbnail) GUILayout.Box(defaultImg, GUILayout.Height(100), GUILayout.Width(100));
							else GUILayout.Box(entryCache.itemThumbnail.texture, GUILayout.Height(100), GUILayout.Width(100));
							ToggleBoxSkin(false);
							DrawImageEditButton(GUILayoutUtility.GetLastRect(), ref entryCache.itemThumbnail, 
				EditorGUIUtility.GetControlID(FocusType.Passive));
							GUILayout.FlexibleSpace();
						GUILayout.EndVertical();
						GUILayout.BeginVertical();
							EditorGUI.BeginChangeCheck();
							entryCache.itemID =  EditorGUILayout.TextField("Item ID", entryCache.itemID);
							GUILayout.Label("Item Description");
							entryCache.itemBasicDescription =  EditorGUILayout.TextArea(entryCache.itemBasicDescription, GUILayout.Height(100));
							if (EditorGUI.EndChangeCheck()) OnEntryValueChanged();
						GUILayout.EndVertical();
					GUILayout.EndHorizontal();
					ToggleBoxSkin(true);
					if (!entryCache.itemImage) GUILayout.Box(defaultImg, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
					else GUILayout.Box(entryCache.itemImage.texture, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));	
					ToggleBoxSkin(false);
					DrawImageEditButton(GUILayoutUtility.GetLastRect(), ref entryCache.itemImage, EditorGUIUtility.GetControlID(FocusType.Passive));
				GUILayout.EndVertical();		
			GUILayout.EndScrollView();

		}

		static Vector2 editorScrollPos;
		void DrawEditor(){
			dDrawEditorContent.Invoke();
		}
		
		static GUISkin defaultSkin;
		
		static void ToggleBoxSkin(bool toggle){
			if (toggle){
				defaultSkin = GUI.skin;
				GUI.skin = itemEditorSkin;
			} 
			else{
				GUI.skin = defaultSkin;
			}
		}

#endregion


		#region Sidebar

		static List<CollectionDataEntry> filteredList;
		static string filter = "", activeFilter = ""; 
		static int selected = -1, selectedOld;	
		static List<string> itemNamesCache, filteredItemNamesCache; 

		delegate void DrawSidebarContent();
		static DrawSidebarContent dDrawSidebarContent;

		//Worker method compare filter string to id of every items in the item list of the database. 
		static void FilterWorker(string idFilter){
			OnFilteringBegin();
			if (idFilter == "") filteredList = new List<CollectionDataEntry>(databaseCache.itemList);
			else filteredList = new List<CollectionDataEntry>();
			filteredItemNamesCache.Clear();
			if (CollectionDataEditorDebugging) Debug.Log("CollectionDataEditor:: FilterWorker():: filteredItemNamesCache cleared.");
			for (int i = 0; i < databaseCache.itemList.Count; i++){
				if (databaseCache.itemList[i].itemID.Contains(idFilter)){
					filteredList.Add(databaseCache.itemList[i]);
					filteredItemNamesCache.Add(databaseCache.itemList[i].itemID);
				}
			}
			activeFilter = idFilter;
			OnFilteringDone();
		}

		static void Sidebar_ShowFiltering(){
			GUILayout.BeginVertical(GUILayout.ExpandHeight(true), GUILayout.ExpandWidth(true));
				GUILayout.Label("Filtering...");
			GUILayout.EndVertical();
		}

		static void Sidebar_ShowNoResult(){
			GUILayout.BeginVertical(GUILayout.ExpandHeight(true), GUILayout.ExpandWidth(true));
				GUILayout.FlexibleSpace();
					GUILayout.Label("No result");
				GUILayout.FlexibleSpace();
			GUILayout.EndVertical();
		}

		static void Sidebar_ShowDatabaseEmpty(){
			GUILayout.BeginVertical(GUILayout.ExpandHeight(true), GUILayout.ExpandWidth(true));
				GUILayout.FlexibleSpace();
					GUILayout.Label("Empty");
				GUILayout.FlexibleSpace();
			GUILayout.EndVertical();
		}
		
		static void Sidebar_DrawSelections(){
			selected = GUILayout.SelectionGrid(selected, filteredItemNamesCache.ToArray(), 1); 
		}

		void DrawSidebar(){

			GUILayout.BeginVertical(GUILayout.MaxWidth(200f));
				GUILayout.Space(4);

				GUILayout.Label("Filter");
				EditorGUI.BeginChangeCheck();
				filter = EditorGUILayout.TextField(filter);
				if (EditorGUI.EndChangeCheck()){ //if filter field is changed
					OnFilterFieldChanged();
				}

				scrollpos = GUILayout.BeginScrollView(scrollpos);		
					dDrawSidebarContent.Invoke();
				GUILayout.EndScrollView();

				GUILayout.Space(6);

				GUILayout.BeginHorizontal();
					if (GUILayout.Button("Add New")) OnAddNewButtonClicked();
					if (GUILayout.Button("Delete Selected")) OnDeleteSelectedButtonClicked();
				GUILayout.EndHorizontal();
				GUILayout.Space(4);
			GUILayout.EndVertical();
		}

		#endregion

		int imagePickerID;

		static void DrawImageEditButton(Rect imageRect, ref Sprite target, int pickerID){
			int buttonW = 24, buttonH = 16, margin = 4;
			Rect buttonRect = new Rect(imageRect.left + margin, imageRect.top + margin, buttonW, buttonH);
			GUI.SetNextControlName("BackToHere");
			if (GUI.Button(buttonRect, "..."))
				EditorGUIUtility.ShowObjectPicker<Sprite>(target, false, "", pickerID);

			if (Event.current.commandName == "ObjectSelectorUpdated")
			{
				if (EditorGUIUtility.GetObjectPickerControlID() == pickerID){ 
					target = (Sprite) EditorGUIUtility.GetObjectPickerObject();
					GUI.FocusControl("BackToHere");
					OnEntryValueChanged();
				}
			}
		}

		static void CacheItemNames(){
			if (CollectionDataEditorDebugging) Debug.Log("CollectionDataEditor:: CacheItemNames():: Caching.");
			itemNamesCache = new List<string>();
			for (int i = 0; i < databaseCache.itemList.Count; i++){
				itemNamesCache.Add(databaseCache.itemList[i].itemID);
			}
		}

#region EventHandler?s
		
		static void OnFilterFieldChanged(){
			if (filter == ""){
				filteredList = new List<CollectionDataEntry>(databaseCache.itemList);
				filteredItemNamesCache = new List<string>(itemNamesCache);
				OnFilteringDone();
			}
			//If user remove filter string when filter is activated...
			if (filter != activeFilter && filter == ""){
				filteredList = new List<CollectionDataEntry>(databaseCache.itemList);
				filteredItemNamesCache = new List<string>(itemNamesCache);
				selected = 0;
				activeFilter = filter;
				if (CollectionDataEditorDebugging) Debug.Log("CollectionDataEditor:: OnFilterFieldChanged():: filter is changed and becomes empty. Set filteredItemNamesCache to a cpoy of itemNamesCache, filteredList to a copy of ItemDatabase.cs.ItemList.");
				if (CollectionDataEditorDebugging) Debug.Log("CollectionDataEditor:: OnFilterFieldChanged():: selected is set to 0.");
				OnFilteringDone();

			}
			//If user enter new filter value...
			else if (activeFilter != filter){
				FilterWorker(filter);
				selected = 0;
				if (CollectionDataEditorDebugging) Debug.Log("CollectionDataEditor:: OnFilterFieldChanged():: Detected new filter, selected is set to 0.");
			}
			else if (activeFilter == filter) {

			}
		}

		static void OnAddNewButtonClicked(){

			CollectionDataEntry newEntry = CollectionDataEntry.Create("New Item");
			AddSubAsset(newEntry, databaseCache);
			databaseCache.itemList.Add(newEntry);
			if (CollectionDataEditorDebugging) Debug.Log("CollectionDataEditor:: OnAddNewButtonClicked():: New entry added to itemDatabaseCache.ItemList.");
			CacheItemNames();

			ForceChangeFilterValue("");
			//itemNamesCache.Add("New Item");
			//if (CollectionDataEditorDebugging) Debug.Log("CollectionDataEditor:: OnAddNewButtonClicked():: \"New Item\" added to itemNamesCache to be optimistic.");
			//filteredItemNamesCache = new List<string>(itemNamesCache);

			selected = databaseCache.itemList.Count - 1;		
			LoadEntry(databaseCache.itemList[selected]);

			dDrawEditorContent = EditorContent_DrawEditor;
			dDrawSidebarContent = Sidebar_DrawSelections;

			SaveDatabase();
		}

		static void OnDeleteSelectedButtonClicked(){
			if (CollectionDataEditorDebugging) Debug.Log("CollectionDataEditor:: OnDeleteSelectedButtonClicked():: Currently selected index: " + selected);

			//Optimistic change on UI
			filteredList.Remove(entryCache);
			if (CollectionDataEditorDebugging) Debug.Log("CollectionDataEditor:: OnDeleteSelectedButtonClicked():: Removing itemID cache in filteredItemNames.");
			filteredItemNamesCache.RemoveAt(selected);

			//Actually remove reference and destroy
			if (CollectionDataEditorDebugging) Debug.Log("CollectionDataEditor:: OnDeleteSelectedButtonClicked():: Removing reference of deleted entry in itemDatabase and filterList.");
			databaseCache.itemList.Remove(entryCache);
			DestroyImmediate(entryCache, true);
			if (CollectionDataEditorDebugging) Debug.Log("CollectionDataEditor:: OnDeleteSelectedButtonClicked():: Designated entry destroyed.");

			CacheItemNames();

			if (filteredList.Count > 0) {
				if (CollectionDataEditorDebugging) Debug.Log("CollectionDataEditor:: OnDeleteSelectedButtonClicked():: Loading filteredList[" + (selected - 1) + "]");
				if (selected >= filteredList.Count) selected -= 1;
				LoadEntry(filteredList[selected]);
			}
			else {
				if (activeFilter == "") {
					dDrawSidebarContent = Sidebar_ShowDatabaseEmpty;
					dDrawEditorContent = EditorContent_NoSelection;
				} 
				else {
					if (CollectionDataEditorDebugging) Debug.Log("CollectionDataEditor:: OnDeleteSelectedButtonClicked():: No entry left in filteredList, clearing filter.");
					ForceChangeFilterValue("");
				}
			}

			SaveDatabase();
		}

		static double lastChangeTime;
		static void OnEntryValueChanged(){
			lastChangeTime = EditorApplication.timeSinceStartup;
			EditorApplication.update -= SaveTimer;
			EditorApplication.update += SaveTimer;
			filteredItemNamesCache[selected] = entryCache.itemID;
		}

		void OnDestroy(){
			SaveDatabase();
		}

		static void OnFilteringBegin(){
			dDrawSidebarContent = Sidebar_ShowFiltering;
			if (CollectionDataEditorDebugging) Debug.Log("CollectionDataEditor:: OnFilteringDone():: Set dDrawSidebarContent to ShowFiltering.");

			dDrawEditorContent = EditorContent_Filtering;
			if (CollectionDataEditorDebugging) Debug.Log("CollectionDataEditor:: OnFilteringDone():: Set dDrawEditorContent to Filtering.");
		}

		static void OnFilteringDone(){
			if (filteredList.Count == 0){
				dDrawSidebarContent = Sidebar_ShowNoResult;
				if (CollectionDataEditorDebugging) Debug.Log("CollectionDataEditor:: OnFilteringDone():: Set dDrawSidebarContent to ShowNoResult.");

				dDrawEditorContent = EditorContent_NoSelection;
				if (CollectionDataEditorDebugging) Debug.Log("CollectionDataEditor:: OnFilteringDone():: Set dDrawEditorContent to NoSelection.");
			}
			else {
				dDrawSidebarContent = Sidebar_DrawSelections;
				if (CollectionDataEditorDebugging) Debug.Log("CollectionDataEditor:: OnFilteringDone():: Set dDrawSidebarContent to DrawSelections.");

				dDrawEditorContent = EditorContent_DrawEditor;
				if (CollectionDataEditorDebugging) Debug.Log("CollectionDataEditor:: OnFilteringDone():: Set dDrawEditorContent to DrawEditor.");
			}
		}

		static void OnSidebarSelectionChanged(){
			if (CollectionDataEditorDebugging) Debug.Log("CollectionDataEditor:: OnSidebarSelectionChanged():: Current selectd == " + selected);			

			if (filteredList.Count > 0 && selected < filteredList.Count){
				GUIUtility.keyboardControl = 0;
				LoadEntry(filteredList[selected]);
				selectedOld = selected;
			}
			else{
				if (CollectionDataEditorDebugging) Debug.Log("CollectionDataEditor:: OnSidebarSelectionChanged():: filteredList is empty. LoadEntry Cancelled.");
			}
		}


		static void OnExportButtonClicked(){
			File.WriteAllText(EditorUtility.SaveFilePanelInProject("Save as JSON file", "export.json", "json", "Select where to save"), databaseCache.ExportToJsonArray());
			AssetDatabase.Refresh();
		}

		static void OnImportButtonClicked(){
			string path = EditorUtility.OpenFilePanelWithFilters( "Open JSON file", Application.dataPath, new string[]{ "Json File", "json"});
			if (string.IsNullOrEmpty(path)) return;
			string temp = File.ReadAllText(path);
			Debug.Log(temp);
			databaseCache.CreateFromJsonArray( databaseCache.ParseJsonArray(temp));
			CacheItemNames();
			ForceChangeFilterValue("");

		}

#endregion

		static void SaveTimer(){
			double temp = EditorApplication.timeSinceStartup - lastChangeTime;
			if (temp > 2){
				if (CollectionDataEditorDebugging) Debug.Log("Delayed saving done after: " + temp);
				SaveDatabase();
				EditorApplication.update -= SaveTimer;
			}
		}

		static public void ForceChangeFilterValue(string filterValue){
			filter = filterValue;
			OnFilterFieldChanged();
		}

	}
}