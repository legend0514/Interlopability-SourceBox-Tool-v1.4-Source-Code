using System.Drawing.Drawing2D;
namespace SourceBox;
public sealed class SceneCanvas:Control {
 bool characters;public bool Characters {get=>characters;set{if(characters==value)return;characters=value;hover=focus="";regions.Clear();Invalidate();}}
 public bool ReducedMotion,Busy;public string LiveSummary="Scanning installed games…";
 public readonly HashSet<string> Selected=new();public HashSet<string> Available=new(),Installed=new();public Dictionary<string,string> Connection=new();
 public event Action<string>? Activate;
 readonly System.Windows.Forms.Timer animation=new(){Interval=33};readonly System.Diagnostics.Stopwatch clock=System.Diagnostics.Stopwatch.StartNew();
 readonly Dictionary<string,Image> weaponArt=new();string artDirectory="";
 readonly Dictionary<string,RectangleF> regions=new();string hover="",focus="";
 static readonly Color cyan=Color.FromArgb(85,218,236),white=Color.FromArgb(226,228,230),muted=Color.FromArgb(129,135,140);
 float scale=1,offsetX,offsetY;
 public SceneCanvas(){SetStyle(ControlStyles.AllPaintingInWmPaint|ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.Selectable,true);TabStop=true;BackColor=Color.Black;AccessibleName="SourceBox";AccessibleDescription="Use arrow keys to choose an item and Enter to activate it. Escape returns home.";animation.Tick+=(_,_)=>{if(Visible&&!ReducedMotion&&FindForm()?.WindowState!=FormWindowState.Minimized)Invalidate();};animation.Start();}
 public void LoadWeaponArt(string directory){if(artDirectory==directory&&weaponArt.Count==6)return;if(artDirectory!=directory){foreach(var image in weaponArt.Values)image.Dispose();weaponArt.Clear();}artDirectory=directory;foreach(var id in Discovery.Defs.Select(g=>g.Id)){if(weaponArt.ContainsKey(id))continue;var file=Path.Combine(directory,id+".png");if(File.Exists(file))try{using var image=Image.FromFile(file);weaponArt[id]=new Bitmap(image);}catch{}}}
 protected override void Dispose(bool disposing){if(disposing){animation.Dispose();foreach(var image in weaponArt.Values)image.Dispose();}base.Dispose(disposing);}
 void TextAt(Graphics g,string text,float x,float y,float size,Color color,bool bold=false){using var f=new Font("Segoe UI",size,bold?FontStyle.Bold:FontStyle.Regular,GraphicsUnit.Pixel);using var b=new SolidBrush(color);g.DrawString(Localization.T(text),f,b,x,y);}
 void CenterText(Graphics g,string text,float x,float y,float size,Color color){using var f=new Font("Segoe UI",size,FontStyle.Regular,GraphicsUnit.Pixel);using var b=new SolidBrush(color);text=Localization.T(text);var sz=g.MeasureString(text,f);g.DrawString(text,f,b,x-sz.Width/2,y);}
 protected override void OnPaint(PaintEventArgs e){
  base.OnPaint(e);var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;g.TextRenderingHint=System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;g.Clear(Color.Black);
  scale=Math.Min(Width/1200f,Height/720f);offsetX=(Width-1200*scale)/2;offsetY=(Height-720*scale)/2;
  var save=g.Save();g.TranslateTransform(Width/2f,Height/2f);g.RotateTransform(15);float tile=120*scale;
  for(int y=-12;y<12;y++)for(int x=-15;x<15;x++)if(((x+y)&1)==0){using var b=new SolidBrush(Color.FromArgb((x+y)%3==0?23:19,(x+y)%3==0?23:19,(x+y)%3==0?24:20));g.FillRectangle(b,x*tile,y*tile,tile-1,tile-1);}g.Restore(save);
  using(var gradient=new LinearGradientBrush(new Rectangle(0,0,Math.Max(1,Width),Math.Max(1,Height)),Color.FromArgb(110,0,0,0),Color.FromArgb(0,0,0,0),90))g.FillRectangle(gradient,ClientRectangle);
  g.TranslateTransform(offsetX,offsetY);g.ScaleTransform(scale,scale);regions.Clear();
  TextAt(g,"SOURCEBOX",46,32,22,white,true);
  Nav(g,"HOME","home",782,42);Nav(g,"CHARACTERS","characters",855,42);Nav(g,"CONTROL CENTER","controls",982,42);
  double t=ReducedMotion?0:clock.Elapsed.TotalSeconds;
  if(Characters)DrawCharacters(g,t);else DrawHome(g,t);
  TextAt(g,LiveSummary,46,680,11,muted);TextAt(g,"1.5.1-beta.1",996,680,10,muted);
  if(Focused&&!string.IsNullOrEmpty(focus)&&regions.TryGetValue(focus,out var box)){using var p=new Pen(Color.FromArgb(150,cyan),1){DashStyle=DashStyle.Dot};g.DrawRectangle(p,box.X,box.Y,box.Width,box.Height);}
 }
 void Nav(Graphics g,string label,string id,float x,float y){regions[id]=new(x-5,y-6,Math.Max(label.Length,Localization.T(label).Length)*7+13,30);TextAt(g,label,x,y,11,hover==id?cyan:muted);}
 float Bob(double t,int phase)=>(float)Math.Sin(t*.7+phase)*7;
 void DrawHome(Graphics g,double t){
  
  
  var a=g.Save();g.TranslateTransform(500,350+Bob(t,0));g.RotateTransform((float)Math.Sin(t*.35)*3);Cube(g,hover=="enter"?cyan:Color.FromArgb(78,80,81));g.Restore(a);
  var b=g.Save();g.TranslateTransform(651,378+Bob(t,2));Sphere(g,hover=="library"?Color.FromArgb(244,55,54):Color.FromArgb(79,83,84));g.Restore(b);
  var c=g.Save();g.TranslateTransform(1080,590+Bob(t,4));Cone(g,hover=="settings"?cyan:Color.FromArgb(79,81,82));g.Restore(c);
  regions["enter"]=new(400,245,204,220);regions["library"]=new(572,295,160,167);regions["settings"]=new(1030,520,100,125);
  string label=hover switch{"enter"=>"CHARACTERS","library"=>"GAME LIBRARY","settings"=>"SETTINGS",_=>""};if(label.Length>0)CenterText(g,label,590,500,11,cyan);
  regions["begin"]=new(47,567,273,58);TextAt(g,"ENTER SOURCEBOX   →",47,579,17,hover=="begin"?cyan:white,true);
 }
 void DrawCharacters(Graphics g,double t){
  TextAt(g,"CHARACTERS",46,111,27,white,true);
  string[] ids={"hl2","tf2","hl1mp","gmod","portal2","cs2"};string[] titles={"GORDON FREEMAN","SOLDIER","GORDON · DEATHMATCH","GMOD","CHELL","COUNTER-STRIKE"};string[] captions={"Half-Life 2  /  Gravity gun","Team Fortress 2  /  Rocket launcher","Half-Life Deathmatch: Source  /  Crowbar","Garry's Mod  /  Physics gun","Portal 2  /  Portal gun","Counter-Strike 2  /  Rifle"};
  for(int i=0;i<6;i++){
   var id=ids[i];float x=215+(i%3)*382,y=i<3?275:480;bool chosen=Selected.Contains(id),hot=hover==id;regions[id]=new(x-157,y-70,314,173);
   if(chosen||hot){using var path=new GraphicsPath();path.AddEllipse(x-116,y-58,232,110);using var glow=new PathGradientBrush(path){CenterColor=Color.FromArgb(chosen?30:17,cyan),SurroundColors=new[]{Color.Transparent}};g.FillPath(glow,path);}
   var state=g.Save();g.TranslateTransform(x,y+Bob(t,i));g.RotateTransform(-12+(float)Math.Sin(t*.35+i)*3);g.ScaleTransform(1.13f,1.13f);
   if(weaponArt.TryGetValue(id,out var art))g.DrawImage(art,new RectangleF(-155,-96,310,181));else CenterText(g,"ARTWORK UNAVAILABLE",0,0,11,muted);g.Restore(state);
   CenterText(g,(chosen?"●  ":"")+titles[i],x,y+64,13,chosen||hot?cyan:white);
   var live=Connection.GetValueOrDefault(id);var text=live?.StartsWith("Connected")==true?"CONNECTED":Available.Contains(id)?"AVAILABLE":Installed.Contains(id)?"INSTALLED · SETUP REQUIRED":"NOT INSTALLED";
   CenterText(g,text,x,y+104,9,text=="CONNECTED"?Color.FromArgb(115,170,139):muted);
  }
  TextAt(g,"←  BACK",47,624,12,hover=="home"?cyan:muted);regions["back"]=new(42,613,100,35);
  
  string action=Busy?"CONNECTING…":Selected.Count==0?"SELECT A CHARACTER":Localization.Format(Selected.Count==1?"LAUNCH {0} GAME   →":"LAUNCH {0} GAMES   →",Selected.Count);
  regions["launch"]=new(905,615,250,48);using(var p=new Pen(Color.FromArgb(hover=="launch"?180:75,cyan),1))g.DrawRectangle(p,905,615,250,48);CenterText(g,action,1030,630,13,Busy||Selected.Count==0?muted:cyan);
 }
 static Color Shade(Color c,float k)=>Color.FromArgb(Math.Clamp((int)(c.R*k),0,255),Math.Clamp((int)(c.G*k),0,255),Math.Clamp((int)(c.B*k),0,255));
 static void Poly(Graphics g,Color color,params float[] xy){using var b=new SolidBrush(color);g.FillPolygon(b,Enumerable.Range(0,xy.Length/2).Select(i=>new PointF(xy[i*2],xy[i*2+1])).ToArray());}
 static void Cube(Graphics g,Color c){Poly(g,Shade(c,1.45f),-80,-60,-13,-99,91,-58,46,-9);Poly(g,Shade(c,.48f),-80,-60,46,-9,13,97,-102,43);Poly(g,Shade(c,.72f),46,-9,91,-58,61,44,13,97);}
 static void Sphere(Graphics g,Color c){using var p=new GraphicsPath();p.AddEllipse(-74,-74,148,148);using var b=new PathGradientBrush(p){CenterPoint=new PointF(-33,-40),CenterColor=Shade(c,1.8f),SurroundColors=new[]{Shade(c,.23f)}};g.FillPath(b,p);}
 static void Cone(Graphics g,Color c){using var b=new LinearGradientBrush(new Rectangle(-45,-60,90,95),Shade(c,.25f),Shade(c,1.5f),0f);g.FillPolygon(b,new[]{new PointF(-45,35),new PointF(8,-60),new PointF(45,40)});using var foot=new SolidBrush(Shade(c,.3f));g.FillEllipse(foot,-45,24,90,26);}
 protected override void OnMouseMove(MouseEventArgs e){base.OnMouseMove(e);var p=new PointF((e.X-offsetX)/scale,(e.Y-offsetY)/scale);var next=regions.FirstOrDefault(x=>x.Value.Contains(p)).Key??"";if(next!=hover){hover=next;Cursor=hover.Length>0?Cursors.Hand:Cursors.Default;Invalidate();}}
 protected override void OnMouseLeave(EventArgs e){base.OnMouseLeave(e);hover="";Invalidate();}
 internal string HitTest(Point point){float s=Math.Min(Width/1200f,Height/720f);if(s<=0)return "";var p=new PointF((point.X-(Width-1200*s)/2)/s,(point.Y-(Height-720*s)/2)/s);return regions.FirstOrDefault(x=>x.Value.Contains(p)).Key??"";}
 protected override void OnMouseClick(MouseEventArgs e){base.OnMouseClick(e);if(e.Button!=MouseButtons.Left)return;Focus();var target=HitTest(e.Location);if(target.Length>0){focus=hover=target;Activate?.Invoke(target);}}
 protected override bool IsInputKey(Keys keyData)=>keyData is Keys.Left or Keys.Right or Keys.Up or Keys.Down||base.IsInputKey(keyData);
 protected override void OnKeyDown(KeyEventArgs e){base.OnKeyDown(e);var keys=regions.Keys.ToArray();if(e.KeyCode==Keys.Escape){Activate?.Invoke("home");return;}if(keys.Length==0)return;if(e.KeyCode is Keys.Left or Keys.Right or Keys.Up or Keys.Down){int index=Array.IndexOf(keys,focus);index=(index+(e.KeyCode is Keys.Left or Keys.Up?-1:1)+keys.Length)%keys.Length;focus=hover=keys[index];Invalidate();e.Handled=true;}if(e.KeyCode is Keys.Enter or Keys.Space){if(focus.Length>0)Activate?.Invoke(focus);e.Handled=true;}}
}




