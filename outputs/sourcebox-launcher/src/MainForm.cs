using System.Diagnostics;
namespace SourceBox;
public sealed class MainForm:Form {
 readonly SceneCanvas scene=new(){Dock=DockStyle.Fill};
 readonly ListView destinations=new();readonly Label destinationStatus=new();
 readonly Runtime runtime;readonly Label status=new(),hero=new(),engines=new();readonly TextBox timeline=new(),details=new();
 readonly DataGridView library=new();readonly ListBox processes=new();readonly TabControl tabs=new();readonly Button start=new(),stop=new(),cancel=new();
 readonly Dictionary<string,TextBox> settingsFields=new();readonly NumericUpDown fps=new();readonly ComboBox preferred=new();readonly CheckBox scan=new(),verbose=new(),reduced=new();
 readonly System.Windows.Forms.Timer timer=new(){Interval=2000};CancellationTokenSource? operation;bool refreshing,busy;
 readonly HashSet<string> selected=new();readonly Color orange=Color.FromArgb(243,137,55),panel=Color.FromArgb(31,34,38),ink=Color.FromArgb(226,228,230);
 protected override void OnHandleCreated(EventArgs e){base.OnHandleCreated(e);
  if(!GameWindowLayout.RegisterHotKey(Handle,7301,0x4003,0x20))runtime.Log("Ctrl+Alt+Space is already in use; Alt+Tab can still release game focus.");
  if(!GameWindowLayout.RegisterHotKey(Handle,7302,0x4003,0x27))runtime.Log("Ctrl+Alt+Right is already in use; use Alt+Tab to switch games.");
 }
 protected override void OnHandleDestroyed(EventArgs e){GameWindowLayout.UnregisterHotKey(Handle,7301);GameWindowLayout.UnregisterHotKey(Handle,7302);base.OnHandleDestroyed(e);}
 protected override void WndProc(ref Message m){
  if(m.Msg==0x0312){if(m.WParam.ToInt32()==7301){GameWindowLayout.ReleaseTo(Handle);return;}if(m.WParam.ToInt32()==7302){runtime.Layout.Next(runtime.Games);return;}}
  base.WndProc(ref m);
 }
 public MainForm(Runtime r,bool autoStart=false){
  runtime=r;runtime.Log("Ctrl+Alt+Space releases the mouse to SourceBox. Ctrl+Alt+Right switches games.");foreach(var id in r.Settings.SelectedGames)selected.Add(id);
  Icon=Icon.ExtractAssociatedIcon(Environment.ProcessPath!);
  Text="SourceBox";Size=new(1120,780);MinimumSize=new(950,650);StartPosition=FormStartPosition.CenterScreen;BackColor=Color.FromArgb(20,22,25);ForeColor=ink;Font=new("Segoe UI",10);
  var outer=new TableLayoutPanel{Dock=DockStyle.Fill,RowCount=3,ColumnCount=1,Padding=new(20)};outer.RowStyles.Add(new(SizeType.Absolute,76));outer.RowStyles.Add(new(SizeType.Percent,100));outer.RowStyles.Add(new(SizeType.Absolute,42));Controls.Add(outer);
  var brand=new Label{Text="SOURCEBOX",Dock=DockStyle.Fill,Font=new("Segoe UI Semibold",18),ForeColor=orange};outer.Controls.Add(brand,0,0);brand.Cursor=Cursors.Hand;brand.Click+=(_,_)=>{scene.Characters=false;scene.Visible=true;scene.BringToFront();scene.Focus();};
  tabs.Dock=DockStyle.Fill;outer.Controls.Add(tabs,0,1);status.Dock=DockStyle.Fill;status.TextAlign=ContentAlignment.MiddleLeft;outer.Controls.Add(status,0,2);
  var home=Page("HOME");var layout=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false,Padding=new(22),AutoScroll=true};home.Controls.Add(layout);
  hero.Size=new(900,100);hero.Font=new("Segoe UI Semibold",23);layout.Controls.Add(hero);
  engines.Size=new(900,90);layout.Controls.Add(engines);
  start.Text="ENTER SOURCEBOX";Style(start);start.Width=260;start.Height=52;start.Click+=async(_,_)=>await StartSelected();layout.Controls.Add(start);
  var note=new Label{Text="Select your games and start a session.",Size=new(900,85),Margin=new(0,24,0,0)};layout.Controls.Add(note);
  var lib=Page("LIBRARY");var lp=new TableLayoutPanel{Dock=DockStyle.Fill,RowCount=3};lp.RowStyles.Add(new(SizeType.Percent,100));lp.RowStyles.Add(new(SizeType.Absolute,78));lp.RowStyles.Add(new(SizeType.Absolute,48));lib.Controls.Add(lp);
  library.Dock=DockStyle.Fill;library.BackgroundColor=panel;library.BorderStyle=BorderStyle.None;library.AllowUserToAddRows=false;library.AllowUserToDeleteRows=false;library.RowHeadersVisible=false;library.SelectionMode=DataGridViewSelectionMode.FullRowSelect;library.MultiSelect=false;library.AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill;library.EnableHeadersVisualStyles=false;library.ColumnHeadersDefaultCellStyle.BackColor=Color.FromArgb(43,47,52);library.ColumnHeadersDefaultCellStyle.ForeColor=ink;library.DefaultCellStyle.BackColor=panel;library.DefaultCellStyle.ForeColor=ink;library.DefaultCellStyle.SelectionBackColor=Color.FromArgb(73,57,43);library.GridColor=Color.FromArgb(48,51,55);library.RowTemplate.Height=42;
  library.Columns.Add(new DataGridViewCheckBoxColumn{Name="Select",FillWeight=30});foreach(var name in new[]{"Game","Engine","Installation","Setup","Connection","World"})library.Columns.Add(name,name);library.Columns["Game"].FillWeight=185;library.Columns["Engine"].FillWeight=65;foreach(DataGridViewColumn c in library.Columns)c.ReadOnly=c.Index!=0;
  library.CurrentCellDirtyStateChanged+=(_,_)=>{if(library.IsCurrentCellDirty)library.CommitEdit(DataGridViewDataErrorContexts.Commit);};library.CellValueChanged+=(_,e)=>{if(refreshing||e.RowIndex<0||e.ColumnIndex!=0)return;var row=library.Rows[e.RowIndex];var id=(string)row.Tag!;if(Convert.ToBoolean(row.Cells[0].Value))selected.Add(id);else selected.Remove(id);runtime.Layout.Monitor=Screen.FromHandle(Handle).Bounds;runtime.Settings.SelectedGames=selected.ToArray();runtime.Settings.Save();scene.Selected.Clear();scene.Selected.UnionWith(selected);scene.Invalidate();};library.SelectionChanged+=(_,_)=>UpdateDetails();lp.Controls.Add(library,0,0);
  details.Multiline=true;details.ReadOnly=true;details.Dock=DockStyle.Fill;details.BackColor=panel;details.ForeColor=ink;details.BorderStyle=BorderStyle.None;lp.Controls.Add(details,0,1);
  var libButtons=new FlowLayoutPanel{Dock=DockStyle.Fill};lp.Controls.Add(libButtons,0,2);
  Add(libButtons,"Scan installed games",async()=>await RefreshAsync());Add(libButtons,"Choose executable",()=>{var id=SelectedGame();if(id==null)return Task.CompletedTask;using var d=new OpenFileDialog{Filter="Windows executable|*.exe"};if(d.ShowDialog()==DialogResult.OK){runtime.Settings.Executables[id]=d.FileName;runtime.Settings.Save();return RefreshAsync();}return Task.CompletedTask;});Add(libButtons,"Open game folder",()=>{var g=runtime.Games.FirstOrDefault(x=>x.Def.Id==SelectedGame());OpenFolder(g?.Root??"");return Task.CompletedTask;});Add(libButtons,"Start selected",StartSelected);Add(libButtons,"Set up crossplay",async()=>{if(selected.Count==0)throw new Exception("Select at least one game first.");var ids=selected.ToArray();await Run(ct=>Task.Run(()=>runtime.PrepareCrossplay(ids,ct),ct));});
  var maps=Page("DESTINATIONS");destinationStatus.Dock=DockStyle.Bottom;destinationStatus.Height=64;destinationStatus.Padding=new(8);maps.Controls.Add(destinationStatus);
  destinations.Dock=DockStyle.Fill;destinations.View=View.Details;destinations.FullRowSelect=true;destinations.MultiSelect=false;destinations.BackColor=panel;destinations.ForeColor=ink;destinations.BorderStyle=BorderStyle.None;
  destinations.Columns.Add("Destination",250);destinations.Columns.Add("Map file",95);destinations.Columns.Add("Content",200);destinations.Columns.Add("Here now",240);maps.Controls.Add(destinations);destinations.BringToFront();
  var sessions=Page("SESSIONS");var sp=new TableLayoutPanel{Dock=DockStyle.Fill,RowCount=2};sp.RowStyles.Add(new(SizeType.Percent,100));sp.RowStyles.Add(new(SizeType.Absolute,55));sessions.Controls.Add(sp);processes.Dock=DockStyle.Fill;processes.BackColor=panel;processes.ForeColor=ink;sp.Controls.Add(processes);
  var sb=new FlowLayoutPanel{Dock=DockStyle.Fill};sp.Controls.Add(sb,0,1);stop.Text="Stop session";Style(stop);stop.Click+=async(_,_)=>{operation?.Cancel();await Run(async ct=>await runtime.Stop());};sb.Controls.Add(stop);cancel.Text="Cancel launch";Style(cancel);cancel.Click+=(_,_)=>operation?.Cancel();sb.Controls.Add(cancel);
  Add(sb,"Restart stopped game",async()=>{if(processes.SelectedItem is ComponentItem item)await Run(ct=>runtime.RestartFailed(item.Id,ct));else throw new Exception("Select an owned failed component.");});
  var settings=Page("SETTINGS");var table=new TableLayoutPanel{Dock=DockStyle.Top,AutoSize=true,ColumnCount=3,Padding=new(14)};table.ColumnStyles.Add(new(SizeType.Absolute,180));table.ColumnStyles.Add(new(SizeType.Percent,100));table.ColumnStyles.Add(new(SizeType.Absolute,95));settings.Controls.Add(table);
  int index=0;foreach(var pair in new[]{("ProjectRoot","SourceBox folder"),("SdkRoot","Game runtime folder"),("SteamRoot","Steam folder"),("NodePath","Node.js"),("WeaponArtFolder","Menu artwork")}){
   table.Controls.Add(new Label{Text=pair.Item2,AutoSize=true,Margin=new(3,10,3,10)},0,index);var box=new TextBox{Dock=DockStyle.Fill,Text=GetSetting(pair.Item1),BackColor=panel,ForeColor=ink,BorderStyle=BorderStyle.FixedSingle};settingsFields[pair.Item1]=box;table.Controls.Add(box,1,index);var browse=new Button{Text="Browse",AutoSize=true};var key=pair.Item1;browse.Click+=(_,_)=>{if(key=="NodePath"){using var d=new OpenFileDialog{Filter="Node.js|node.exe"};if(d.ShowDialog()==DialogResult.OK)box.Text=d.FileName;}else{using var d=new FolderBrowserDialog();if(d.ShowDialog()==DialogResult.OK)box.Text=d.SelectedPath;}};table.Controls.Add(browse,2,index++);
  }
  var pathControls=table.Controls.Cast<Control>().ToArray();var showPaths=new CheckBox{Text="File locations",AutoSize=true,Checked=!File.Exists(Path.Combine(runtime.Settings.ProjectRoot,"core.mjs"))};foreach(var control in pathControls)control.Visible=showPaths.Checked;showPaths.CheckedChanged+=(_,_)=>{foreach(var control in pathControls)control.Visible=showPaths.Checked;};table.Controls.Add(showPaths,1,index++);preferred.DropDownStyle=ComboBoxStyle.DropDownList;preferred.DisplayMember="Title";preferred.ValueMember="Id";preferred.DataSource=Discovery.Defs;preferred.SelectedValue=runtime.Settings.PreferredGame;table.Controls.Add(new Label{Text="Preferred game",AutoSize=true},0,index);table.Controls.Add(preferred,1,index++);fps.Minimum=30;fps.Maximum=240;fps.Value=Math.Clamp(runtime.Settings.FpsLimit,30,240);table.Controls.Add(new Label{Text="Frame limit",AutoSize=true},0,index);table.Controls.Add(fps,1,index++);
  scan.Text="Scan games on startup";scan.AutoSize=true;scan.Checked=true;verbose.Text="Detailed logs";verbose.AutoSize=true;verbose.Checked=runtime.Settings.VerboseLogging;table.Controls.Add(verbose,1,index++);reduced.Text="Reduced motion — keep floating scenes still";reduced.AutoSize=true;reduced.Checked=runtime.Settings.ReducedMotion;table.Controls.Add(reduced,1,index++);
  var limits=new Label{Text="Closing SourceBox leaves your games running.\nNetwork ports: 27841, 27842 and 27015–27020.",AutoSize=true,MaximumSize=new(700,0),Margin=new(3,14,3,14)};table.Controls.Add(limits,1,index++);
  var setButtons=new FlowLayoutPanel{AutoSize=true};table.Controls.Add(setButtons,1,index++);Add(setButtons,"Save settings",async()=>{SaveSettings();await RefreshAsync();});Add(setButtons,"Reset defaults",async()=>{if(File.Exists(Settings.FilePath))File.Copy(Settings.FilePath,Settings.FilePath+".before-reset",true);runtime.Settings=Settings.Defaults();runtime.Settings.Save();foreach(var p in settingsFields)p.Value.Text=GetSetting(p.Key);fps.Value=60;preferred.SelectedValue=runtime.Settings.PreferredGame;scan.Checked=true;verbose.Checked=false;reduced.Checked=false;await RefreshAsync();});
  Add(setButtons,"Export app ZIP",async()=>{
   var output=Path.Combine(Settings.Home,"exports","SourceBox-1.5.0-beta.1-"+DateTime.Now.ToString("yyyyMMdd-HHmmssfff")+".zip");
   await Run(async ct=>{runtime.Log("Packaging SourceBox for sharing…");await Task.Run(()=>DesktopInstall.ExportZip(AppContext.BaseDirectory,output),ct);runtime.Log("Shareable app ZIP saved: "+output);});
   if(File.Exists(output))OpenFolder(Path.GetDirectoryName(output)!);
  });
  var logs=Page("LOGS");var logLayout=new TableLayoutPanel{Dock=DockStyle.Fill,RowCount=2};logLayout.RowStyles.Add(new(SizeType.Percent,100));logLayout.RowStyles.Add(new(SizeType.Absolute,50));logs.Controls.Add(logLayout);timeline.Multiline=true;timeline.ReadOnly=true;timeline.ScrollBars=ScrollBars.Vertical;timeline.Dock=DockStyle.Fill;timeline.BackColor=panel;timeline.ForeColor=ink;timeline.Font=new("Consolas",10);logLayout.Controls.Add(timeline);var lb=new FlowLayoutPanel{Dock=DockStyle.Fill};logLayout.Controls.Add(lb,0,1);Add(lb,"Open session logs",()=>{OpenFolder(runtime.SessionDir);return Task.CompletedTask;});Add(lb,"Open game logs",()=>{OpenFolder(Path.Combine(runtime.Settings.ProjectRoot,"logs"));return Task.CompletedTask;});Add(lb,"Copy diagnostics",()=>{Clipboard.SetText(runtime.Diagnostics());runtime.Log("Support details copied.");return Task.CompletedTask;});Add(lb,"Save support report",()=>{runtime.Log("Support report saved: "+runtime.Export());return Task.CompletedTask;});
  tabs.SelectedIndexChanged+=(_,_)=>{if(tabs.SelectedTab?.Name=="HOME"&&!scene.Visible){scene.Characters=false;scene.Visible=true;scene.BringToFront();}};Controls.Add(scene);scene.VisibleChanged+=(_,_)=>outer.Visible=!scene.Visible;outer.Visible=false;scene.BringToFront();scene.Activate+=async id=>{
   if(id is "enter" or "begin" or "characters"){scene.Characters=true;scene.Visible=true;scene.Invalidate();return;}
   if(id is "home" or "back"){scene.Characters=false;scene.Visible=true;scene.Invalidate();return;}
   if(id is "controls" or "library" or "settings"){scene.Visible=false;tabs.SelectedTab=tabs.TabPages[id=="settings"?"SETTINGS":id=="controls"?"SESSIONS":"LIBRARY"];return;}
   if(id=="launch"){if(selected.Count>0&&!busy)await StartSelected();return;}
   if(Discovery.Defs.Any(g=>g.Id==id)){
    if(!selected.Add(id))selected.Remove(id);runtime.Layout.Monitor=Screen.FromHandle(Handle).Bounds;runtime.Settings.SelectedGames=selected.ToArray();runtime.Settings.Save();Render();
   }
  };
  runtime.Changed+=()=>{if(IsHandleCreated&&!IsDisposed)BeginInvoke(new Action(Render));};timer.Tick+=async(_,_)=>{if(!busy)await RefreshAsync();};Shown+=async(_,_)=>{await RefreshAsync();if(!File.Exists(Path.Combine(runtime.Settings.ProjectRoot,"core.mjs"))){runtime.Log("Installed games detected automatically. Crossplay runtime installation is still required.");}timer.Start();if(autoStart)await StartSelected();};FormClosing+=(_,_)=>{timer.Stop();operation?.Cancel();};
 }
 TabPage Page(string name){var p=new TabPage(name){Name=name,BackColor=panel,ForeColor=ink,Padding=new(12)};tabs.TabPages.Add(p);return p;}
 void Style(Button b){b.AutoSize=true;b.FlatStyle=FlatStyle.Flat;b.ForeColor=ink;b.BackColor=Color.FromArgb(64,44,29);b.Padding=new(10,6,10,6);b.Margin=new(4);}
 void Add(Control parent,string text,Func<Task> action){var b=new Button{Text=text};Style(b);b.Click+=async(_,_)=>{try{await action();}catch(Exception e){runtime.Log(e.Message);MessageBox.Show(this,e.Message,"SourceBox",MessageBoxButtons.OK,MessageBoxIcon.Information);}};parent.Controls.Add(b);}
 string GetSetting(string key)=>typeof(Settings).GetProperty(key)!.GetValue(runtime.Settings)?.ToString()??"";
 void SaveSettings(){var proposed=System.Text.Json.JsonSerializer.Deserialize<Settings>(System.Text.Json.JsonSerializer.Serialize(runtime.Settings))!;foreach(var p in settingsFields)typeof(Settings).GetProperty(p.Key)!.SetValue(proposed,p.Value.Text.Trim());proposed.FpsLimit=(int)fps.Value;proposed.PreferredGame=preferred.SelectedValue?.ToString()??runtime.Settings.PreferredGame;proposed.ScanOnOpen=scan.Checked;proposed.VerboseLogging=verbose.Checked;proposed.ReducedMotion=reduced.Checked;proposed.SelectedGames=selected.ToArray();proposed.Save();runtime.Settings=proposed;runtime.CoreLog="";runtime.Log("Settings saved. Existing sessions unchanged.");}
 string? SelectedGame()=>library.SelectedRows.Count>0?library.SelectedRows[0].Tag as string:null;
 void UpdateDetails(){var g=runtime.Games.FirstOrDefault(x=>x.Def.Id==SelectedGame());details.Text=g==null?"":g.Exe+Environment.NewLine+g.Detail;}
 internal async Task StartSelected(){if(selected.Count==0){MessageBox.Show(this,"Select at least one game first.","SourceBox");return;}runtime.Layout.Monitor=Screen.FromHandle(Handle).Bounds;runtime.Settings.SelectedGames=selected.ToArray();runtime.Settings.Save();await Run(ct=>runtime.Start(selected.ToArray(),ct));}
 async Task Run(Func<CancellationToken,Task> action){if(busy){runtime.Log("Wait for the current session action, or cancel it first.");return;}busy=true;operation=new();Render();try{await Task.Run(()=>action(operation.Token));}catch(OperationCanceledException){}catch(Exception e){runtime.Log(e.Message);MessageBox.Show(this,e.Message,"Unable to start session",MessageBoxButtons.OK,MessageBoxIcon.Information);}finally{busy=false;operation.Dispose();operation=null;await RefreshAsync();}}
 async Task RefreshAsync(){if(refreshing)return;refreshing=true;try{await Task.Run(runtime.Refresh);Render();}catch(Exception e){status.Text=e.Message;}finally{refreshing=false;}}
 void Render(){
  bool old=refreshing;refreshing=true;try{
   scene.Selected.Clear();scene.Selected.UnionWith(selected);scene.Installed=runtime.Games.Where(g=>g.Installed).Select(g=>g.Def.Id).ToHashSet();scene.Available=runtime.Games.Where(g=>g.Ready).Select(g=>g.Def.Id).ToHashSet();scene.Connection=runtime.Live.ToDictionary(x=>x.Key,x=>x.Value.State);scene.Busy=busy;scene.ReducedMotion=runtime.Settings.ReducedMotion;scene.LoadWeaponArt(runtime.Settings.WeaponArtFolder);scene.LiveSummary=runtime.State+"  ·  "+runtime.Live.Values.Count(x=>x.State.StartsWith("Connected"))+" connected";scene.Invalidate();
   status.Text="1.5.0-beta.1  ·  "+runtime.State+"  ·  Server: "+runtime.CoreStatus;
   RenderDestinations();
   int connected=runtime.Live.Values.Count(x=>x.State.StartsWith("Connected"));hero.Text=connected>0?$"{connected} clients connected":"SourceBox";
   engines.Text="Source 1  ·  "+runtime.Games.Count(g=>g.Def.Engine=="Source 1"&&g.Ready)+" games ready     |     Source 2  ·  Counter-Strike 2\n"+runtime.CoreStatus;
   var id=SelectedGame();library.Rows.Clear();foreach(var g in runtime.Games){var l=runtime.Live.GetValueOrDefault(g.Def.Id);var i=library.Rows.Add(selected.Contains(g.Def.Id),g.Def.Title,g.Def.Engine,g.Installed?"Installed":"Missing",g.Ready?"Ready":"Setup needed",l?.State??"Not connected",l?.World??"Unknown");library.Rows[i].Tag=g.Def.Id;if(g.Def.Id==id)library.Rows[i].Selected=true;}
   UpdateDetails();var prior=(processes.SelectedItem as ComponentItem)?.Id;processes.Items.Clear();foreach(var group in runtime.Owned.GroupBy(p=>p.Component)){var alive=group.Any(p=>p.Matches());var item=new ComponentItem(group.Key,group.Key+" — "+(alive?"Running":"Stopped"));processes.Items.Add(item);if(prior==item.Id)processes.SelectedItem=item;}if(runtime.Owned.Count==0)processes.Items.Add("No games started by SourceBox this session.");
   timeline.Text=string.Join(Environment.NewLine,runtime.Lines());timeline.SelectionStart=timeline.TextLength;timeline.ScrollToCaret();start.Enabled=!busy;stop.Enabled=!busy;cancel.Enabled=busy;
  }finally{refreshing=old;}
 }
 void RenderDestinations(){
  var chosen=destinations.SelectedItems.Count>0?destinations.SelectedItems[0].Tag as string:null;
  destinations.BeginUpdate();try{
   destinations.Items.Clear();destinations.Groups.Clear();
   foreach(var group in runtime.Catalog.Items.GroupBy(d=>d.Origin)){
    var heading=new ListViewGroup(group.Key,HorizontalAlignment.Left);destinations.Groups.Add(heading);
    foreach(var d in group){
     var players=runtime.Live.Where(p=>p.Value.State=="Connected"&&p.Value.World==d.Id).Select(p=>Discovery.Defs.FirstOrDefault(g=>g.Id==p.Key)?.Title??p.Key);
     var plan=runtime.Content.Worlds.FirstOrDefault(w=>w.Id==d.Id);
     var mode=plan?.Mode=="topology-placeholder"?(plan.TopologyCached?"Fallback not ready":"Map unavailable"):"Installed assets";
     var item=new ListViewItem(new[]{d.Title,d.Source.Length==0?"Built-in":d.SourcePresent?"Found":"Missing",mode,string.Join(" / ",players)},heading){Tag=d.Id};destinations.Items.Add(item);if(d.Id==chosen)item.Selected=true;
    }
   }
   destinationStatus.Text=runtime.Catalog.Error.Length>0?runtime.Catalog.Error:!runtime.Catalog.Enabled?"World destinations are disabled in the project.":"Choose a destination with the portal gun in-game. Map quality varies by game.";
  }finally{destinations.EndUpdate();}
 }
 public void SavePreview(string path){
  var dir=Path.GetDirectoryName(path)!;
  scene.Visible=true;scene.Characters=false;scene.Refresh();using(var shot=new Bitmap(Width,Height)){DrawToBitmap(shot,new Rectangle(0,0,Width,Height));shot.Save(Path.Combine(dir,"scene-home.png"));}
  scene.Characters=true;scene.Refresh();using(var shot=new Bitmap(Width,Height)){DrawToBitmap(shot,new Rectangle(0,0,Width,Height));shot.Save(Path.Combine(dir,"scene-characters.png"));}
  scene.Visible=false;
  foreach(TabPage tab in tabs.TabPages){tabs.SelectedTab=tab;tabs.Refresh();using var shot=new Bitmap(Width,Height);DrawToBitmap(shot,new Rectangle(0,0,Width,Height));shot.Save(Path.Combine(dir,"launcher-"+tab.Text.ToLowerInvariant()+".png"));}
  tabs.SelectedIndex=0;scene.Characters=false;scene.Visible=true;
 }
 static void OpenFolder(string path){if(!Directory.Exists(path))throw new Exception("Folder is unavailable.");Process.Start(new ProcessStartInfo("explorer.exe"){UseShellExecute=true,Arguments='"'+Path.GetFullPath(path)+'"'});}
 sealed record ComponentItem(string Id,string Label){public override string ToString()=>Label;}
}








