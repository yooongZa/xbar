using System.Collections.Immutable;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using NSec.Cryptography;
using XBar.App;
using XBar.Core;
namespace XBar.WindowsTests;
internal static class UpdaterTests
{
    private sealed class Handler(Func<HttpRequestMessage,CancellationToken,Task<HttpResponseMessage>> send) : HttpMessageHandler
    { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token) => send(request,token); }
    private sealed class Installer : IUpdateInstaller
    {
        public int Prepared, Installed; public bool FailPrepare, FailInstall, Wait;
        public async Task PrepareAsync(string path,CancellationToken token) { Prepared++; if(Wait) await Task.Delay(10000,token); if(FailPrepare) throw new CryptographicException(); }
        public Task InstallAsync(string path,IProgress<double> progress) { Installed++; if(FailInstall) throw new IOException(); return Task.CompletedTask; }
    }
    public static async Task Run(Action<string,bool,string?> check)
    {
        using var key=Key.Create(SignatureAlgorithm.Ed25519);
        var source=new UpdateSource(new("https://yooongza.github.io/xbar/windows-updates.json"),Convert.ToBase64String(key.PublicKey.Export(KeyBlobFormat.RawPublicKey)));
        var identity=new UpdateIdentity("LIMOD.XBar.Dev","CN=XBar Development","x64",new(0,3,0,0),26200);
        using var memory=new MemoryStream();
        using(var zip=new ZipArchive(memory,ZipArchiveMode.Create,true)) {
            using(var w=new StreamWriter(zip.CreateEntry("AppxManifest.xml").Open())) w.Write("<Package xmlns='http://schemas.microsoft.com/appx/manifest/foundation/windows10'><Identity Name='LIMOD.XBar.Dev' Publisher='CN=XBar Development' ProcessorArchitecture='x64' Version='0.3.0.1'/></Package>");
            using var s=new StreamWriter(zip.CreateEntry("AppxSignature.p7x").Open()); s.Write("structural fixture only");
        }
        var package=memory.ToArray();
        var release=new UpdateRelease(1,"windows","0.3.0.1","0.3.0",22000,DateTimeOffset.UtcNow,ImmutableDictionary<string,string>.Empty.Add("ko","업데이트 시험").Add("en","Update test"),[new(identity.Name,identity.Publisher,"x64","https://github.com/yooongZa/xbar/releases/download/test/update.msix",package.Length,Convert.ToHexString(SHA256.HashData(package)))]);
        var payload=JsonSerializer.SerializeToUtf8Bytes(release,StateStore.Json);
        var feed=JsonSerializer.SerializeToUtf8Bytes(new UpdateEnvelope(Convert.ToBase64String(payload),Convert.ToBase64String(SignatureAlgorithm.Ed25519.Sign(key,payload))),StateStore.Json);
        var root=Path.Combine(Path.GetTempPath(),"XBar-updater-tests-"+Guid.NewGuid().ToString("N")); var store=new StateStore(root);
        int requests=0,paused=0,resumed=0; bool delay=false, timeout=false;
        var handler=new Handler(async(request,token)=> { requests++; if(timeout) throw new TaskCanceledException(); if(delay) await Task.Delay(10000,token); return new(HttpStatusCode.OK){Content=new ByteArrayContent(request.RequestUri==source.Feed?feed:package)}; });
        var installer=new Installer();
        using var updater=new AppUpdater(store,()=>"ko",()=>paused++,()=>resumed++,identity,new UpdateClient(source,handler),installer);
        void Check(string name,bool value) => check(name,value,null);
        Check("Automatic checks default on",updater.Preferences.AutomaticChecks);
        updater.SetAutomaticChecks(false); Check("Automatic checks persisted separately",store.Read<UpdatePreferences>("updates.json")?.AutomaticChecks==false && !File.Exists(Path.Combine(root,"settings.json")));
        timeout=true; await updater.CheckAsync(true); timeout=false; requests=0; Check("Network timeout is distinguished from user cancel",updater.Phase==UpdatePhase.Error && updater.ErrorKey=="update.network.error");
        delay=true; var pending=updater.CheckAsync(true); await Task.Delay(50); await updater.CheckAsync(true); Check("Concurrent checks coalesce",requests==1); updater.Cancel(); await pending; delay=false;
        Check("Canceled check releases busy state",!updater.Busy && updater.Phase==UpdatePhase.Canceled);
        await updater.CheckAsync(true); Check("Signed new release becomes available without installing",updater.Phase==UpdatePhase.Available && installer.Installed==0);
        var stale=updater.BeginPresentation(); updater.BeginPresentation(); await updater.InstallAsync(stale); Check("Stale presentation cannot download or install",installer.Prepared==0 && updater.Phase==UpdatePhase.Available);
        installer.Wait=true; var installing=updater.InstallAsync(updater.BeginPresentation());
        for(int i=0;i<100 && updater.Phase!=UpdatePhase.Preparing;i++) await Task.Delay(20);
        updater.Cancel(); await installing; installer.Wait=false;
        Check("Cancel during preparation never hands off to OS",installer.Installed==0 && paused==0 && !updater.Busy && updater.Phase==UpdatePhase.Canceled && !File.Exists(Path.Combine(root,"update-install.json")));
        await updater.CheckAsync(true); installer.FailPrepare=true; await updater.InstallAsync(updater.BeginPresentation()); installer.FailPrepare=false;
        Check("Trust failure preserves running collector",updater.Phase==UpdatePhase.Error && installer.Installed==0 && paused==0);
        await updater.CheckAsync(true); installer.FailInstall=true; await updater.InstallAsync(updater.BeginPresentation()); installer.FailInstall=false;
        Check("OS failure resumes collector and removes receipt",updater.Phase==UpdatePhase.Error && paused==1 && resumed==1 && !updater.Busy && !File.Exists(Path.Combine(root,"update-install.json")));
        await updater.InstallAsync(stale); Check("Failed install does not retry without a new choice",installer.Installed==1);
        await updater.CheckAsync(true); updater.Skip(); await updater.CheckAsync(false); Check("Skipped release suppressed by automatic check",updater.Available==null); await updater.CheckAsync(true); Check("Manual check still shows skipped release",updater.Available!=null);
        foreach(var lang in new[]{"ko","en","ja"}) foreach(var theme in new[]{"light","dark"}) {
            Theme.TestMode=theme; Theme.Apply(); var w=new UpdateWindow(updater,lang,updater.BeginPresentation()); w.Show(); w.UpdateLayout();
            Check($"Update dialog {theme} {lang} opens",w.ActualWidth==500 && w.ActualHeight==460); w.Close();
        }
        Theme.TestMode=null; Theme.Apply();
        await updater.CheckAsync(true);
        var choiceWindow = new UpdateWindow(updater,"ko",updater.BeginPresentation()); choiceWindow.Show(); choiceWindow.ResetSession();
        choiceWindow.InstallControl.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
        for(int i=0;i<100 && installer.Installed<2;i++) await Task.Delay(20);
        Check("Install button uses the refreshed presentation and hands off once",installer.Installed==2 && paused==2 && store.Read<UpdateReceipt>("update-install.json")?.Version=="0.3.0.1");
        choiceWindow.AllowClose = true; choiceWindow.Close();
        using var after=new AppUpdater(store,()=>"ko",()=>{},()=>{},identity with{Version=new(0,3,0,1)},new UpdateClient(source,new Handler((_,_)=>Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)))),new Installer());
        Check("Relaunch confirms updated version and keeps preferences",after.Phase==UpdatePhase.Updated && !after.Preferences.AutomaticChecks && !File.Exists(Path.Combine(root,"update-install.json")));
        await after.CheckAsync(true); Check("Unpublished public channel has distinct UI state",after.Phase==UpdatePhase.ChannelPending);
    }
}
