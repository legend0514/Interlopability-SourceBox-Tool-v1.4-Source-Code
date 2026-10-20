namespace SourceBox;

public sealed class SplashForm : Form {
 readonly System.Windows.Controls.MediaElement media=new();
 readonly System.Windows.Forms.Timer deadline=new(){Interval=12000};
 readonly Action<string> log;
 bool finished;
 public SplashForm(Action<string> log){
  this.log=log;Text="SourceBox";BackColor=Color.Black;FormBorderStyle=FormBorderStyle.None;
  StartPosition=FormStartPosition.CenterScreen;ClientSize=new(1104,741);KeyPreview=true;
  var host=new System.Windows.Forms.Integration.ElementHost{Dock=DockStyle.Fill,BackColor=Color.Black};
  media.LoadedBehavior=System.Windows.Controls.MediaState.Manual;
  media.UnloadedBehavior=System.Windows.Controls.MediaState.Close;
  media.Stretch=System.Windows.Media.Stretch.Uniform;media.Volume=1;
  host.Child=media;Controls.Add(host);
  media.MediaOpened+=(_,_)=>log($"Splash opened: {media.NaturalVideoWidth}x{media.NaturalVideoHeight}");
  media.MediaEnded+=(_,_)=>Finish("ended");
  media.MediaFailed+=(_,e)=>Finish("unavailable: "+e.ErrorException.Message);
  media.MouseLeftButtonDown+=(_,_)=>Finish("skipped");
  host.MouseDown+=(_,_)=>Finish("skipped");
  KeyDown+=(_,e)=>{if(e.KeyCode is Keys.Escape or Keys.Space or Keys.Enter){e.Handled=true;Finish("skipped");}};
  media.KeyDown+=(_,e)=>{if(e.Key is System.Windows.Input.Key.Escape or System.Windows.Input.Key.Space or System.Windows.Input.Key.Enter){e.Handled=true;Finish("skipped");}};
  deadline.Tick+=(_,_)=>Finish("playback timeout");
  Shown+=(_,_)=>{
   var path=Path.Combine(AppContext.BaseDirectory,"Assets","sourcebox-intro.mp4");
   if(!File.Exists(path)){Finish("file missing");return;}
   deadline.Start();media.Source=new Uri(path);media.Play();
  };
 }
 void Finish(string reason){if(finished)return;finished=true;log("Splash "+reason);Close();}
 protected override void OnFormClosed(FormClosedEventArgs e){deadline.Stop();media.Stop();media.Close();base.OnFormClosed(e);}
 protected override void Dispose(bool disposing){if(disposing)deadline.Dispose();base.Dispose(disposing);}
}
