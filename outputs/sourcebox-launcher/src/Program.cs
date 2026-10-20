namespace SourceBox;
static class Program {
 [STAThread]static void Main(string[] args){
  if(args.Contains("--install")){DesktopInstall.Run(args.Contains("--quiet"));return;}
  if(args.Contains("--install-check")){
   Directory.CreateDirectory(Settings.Home);
   try{var exe=DesktopInstall.Install(AppContext.BaseDirectory,Path.Combine(Settings.Home,"installation-check"),false);var settings=Settings.Load();DesktopInstall.Verify(Path.GetDirectoryName(exe)!);File.WriteAllText(Path.Combine(Settings.Home,"installation-check.json"),System.Text.Json.JsonSerializer.Serialize(new{status="passed",exe,settings.ProjectRoot,settings.SdkRoot,games=Discovery.Scan(settings)},new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));}
   catch(Exception e){File.WriteAllText(Path.Combine(Settings.Home,"installation-check-error.txt"),e.ToString());Environment.ExitCode=1;}return;
  }
  if(args.Length==2&&args[0]=="--service-host"){ServiceHost.Run(args[1]).GetAwaiter().GetResult();return;}
  if(args.Length==2&&args[0]=="--protect-config"){ConfigProtection.Run(args[1]).GetAwaiter().GetResult();return;}
  if(args.Length==2&&args[0]=="--cs-bootstrap"){CsBootstrap.Run(args[1]).GetAwaiter().GetResult();return;}
  if(args.Contains("--translation-check")){Localization.Validate();return;}
  if(args.Contains("--fixture")){Thread.Sleep(60000);return;}
  if(args.Contains("--live-check")){Environment.ExitCode=Tests.LiveCheck().GetAwaiter().GetResult();return;}
  if(args.Length==2&&args[0]=="--launch-check"){Environment.ExitCode=Tests.LaunchCheck(args[1].Split(',')).GetAwaiter().GetResult();return;}
  if(args.Contains("--setup-check")){
   var settings=Settings.Load();Directory.CreateDirectory(Settings.Home);
   var plans=Discovery.Scan(settings).Select(game=>{try{return CrossplaySetup.Plan(settings,game);}catch(Exception e){return new SetupPlan(game.Def.Id,new(),new(){e.Message});}}).ToArray();
   File.WriteAllText(Path.Combine(Settings.Home,"setup-check.json"),System.Text.Json.JsonSerializer.Serialize(plans,new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));
   Environment.ExitCode=plans.Any(p=>p.Missing.Count>0)?2:0;return;
  }
  if(args.Contains("--prepare-local-assets")){
   var settings=Settings.Load();Directory.CreateDirectory(Settings.Home);
   try{var games=Discovery.Scan(settings);void Log(string message)=>File.AppendAllText(Path.Combine(Settings.Home,"asset-setup.log"),message+Environment.NewLine);SharedAssetPreparation.Prepare(settings,games,CancellationToken.None,Log).GetAwaiter().GetResult();Environment.ExitCode=0;}catch(Exception e){File.WriteAllText(Path.Combine(Settings.Home,"asset-setup-error.txt"),e.ToString());Environment.ExitCode=1;}return;
  }
  if(args.Contains("--prepare-cs2-content")){
   var settings=Settings.Load();Directory.CreateDirectory(Settings.Home);
   try{var games=Discovery.Scan(settings).Where(g=>g.Installed).ToDictionary(g=>g.Def.Id);void Log(string message)=>File.AppendAllText(Path.Combine(Settings.Home,"cs2-setup.log"),message+Environment.NewLine);CsContentPreparation.Prepare(settings,games,CancellationToken.None,Log,true).GetAwaiter().GetResult();Environment.ExitCode=0;}catch(Exception e){File.WriteAllText(Path.Combine(Settings.Home,"cs2-setup-error.txt"),e.ToString());Environment.ExitCode=1;}return;
  }
  if(args.Contains("--self-test")){Directory.CreateDirectory(Settings.Home);Environment.ExitCode=Tests.Run();return;}
  ApplicationConfiguration.Initialize();using var mutex=new Mutex(true,"Local\\SourceBoxLauncherMvp",out bool first);if(!first){MessageBox.Show("SourceBox is already open.");return;}
  var runtime=new Runtime(Settings.Load());
  var autoStart=args.Length==2&&args[0]=="--start-games";
  if(autoStart){var ids=args[1].Split(',').Distinct().ToArray();if(ids.Length==0||ids.Any(id=>!Discovery.Defs.Any(d=>d.Id==id))){Environment.ExitCode=1;return;}runtime.Settings.SelectedGames=ids;}
  if(!args.Contains("--smoke-ui")&&!args.Contains("--no-splash")){using var splash=new SplashForm(line=>runtime.Log(line));splash.ShowDialog();}
  if(args.Contains("--splash-test"))return;
  using var form=new MainForm(runtime,autoStart);
  if(args.Contains("--smoke-ui")){var timer=new System.Windows.Forms.Timer{Interval=6000};timer.Tick+=(_,_)=>{timer.Stop();form.SavePreview(Path.Combine(Settings.Home,"launcher-preview.png"));form.Close();};timer.Start();}
  Application.Run(form);
 }
}



