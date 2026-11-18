using System.IO;
using System.Text.Json;
using NetAdapterManager.Models;
using NetAdapterManager.Services;
using NetAdapterManager.ViewModels;
internal static class Version170Tests
{
    private static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    internal static async Task RunAsync(Action<string, string> pass, Action<string, string> fail)
    {
        async Task Check(string name, Func<Task> test) { try { await test(); pass(name, "Synthetic networking and isolated preferences; no OS writes."); } catch(Exception ex) { fail(name, ex.ToString()); } }
        static NetworkConfig Dhcp(params string[] addresses) => new() { DhcpStaticAddresses = addresses.Select(s => new Ipv4AddressEntry { Address=s, PrefixLength=24, PrefixOrigin="Manual" }).ToList() };
        await Check("DHCP附加地址命令保持租约和网关", () =>
        {
            var before = new AdapterSnapshot { InterfaceIndex=7, Config=Dhcp("192.0.2.10") };
            before.Config.IPv4Addresses.Add(new() { Address="198.51.100.20", PrefixLength=24, Gateway="198.51.100.1", PrefixOrigin="Dhcp" });
            var commands=new List<string[]>();
            NetworkService.ApplyConfiguration(before,Dhcp("192.0.2.10","192.0.2.11"),true,commands.Add);
            Require(commands.Count==2 && commands[0].Contains("dhcpstaticipcoexistence=enabled") && commands[1].Contains("address=192.0.2.11") && commands[1].Contains("skipassource=true"), "Unexpected DHCP/additional commands.");
            Require(commands.All(c=>!c.Contains("source=static") && !c.Contains("source=dhcp") && !c.Any(a=>a.StartsWith("gateway="))), "DHCP lease/gateway was rewritten.");
            return Task.CompletedTask;
        });
        await Check("附加地址失败回滚且复核地址集合", async () =>
        {
            var state=Dhcp("192.0.2.10"); var writes=0;
            AdapterSnapshot Read()=>new() {InterfaceGuid="test",InterfaceIndex=7,Config=state.Clone()};
            var network=new NetworkService(_=>Task.FromResult<IReadOnlyList<AdapterSnapshot>>([Read()]),(_,t,_)=>{state=t.Clone();if(++writes==1)throw new IOException("Simulated partial add");},(_,_)=>Task.CompletedTask,(_,_,_)=>Task.CompletedTask);
            var result=await network.ApplyAsync(Read(),Dhcp("192.0.2.10","192.0.2.11"),OperationSource.Manual);
            Require(!result.Succeeded && result.RolledBack && writes==2 && state.DhcpStaticAddresses.Single().Address=="192.0.2.10","Rollback lost existing additional IP.");
            Require(!NetworkService.MatchesTarget(Read(),Dhcp("192.0.2.11"),true),"Additional IP mismatch accepted.");
            var unknown=Read();unknown.AdditionalAddressStateKnown=false;
            Require(!NetworkService.MatchesTarget(unknown,Dhcp(),true),"Unknown state reported as verified.");
        });
        await Check("跨网卡模板仅应用当前选择并尊重取消", async () =>
        {
            var a=new AdapterSnapshot {InterfaceGuid="a",Name="A",Config=Dhcp()};var b=new AdapterSnapshot {InterfaceGuid="b",Name="B",Config=Dhcp()};var reads=0;var writes=0;
            var network=new NetworkService(_=>{reads++;return Task.FromResult<IReadOnlyList<AdapterSnapshot>>([a,b]);},(_,_,_)=>writes++,(_,_)=>Task.CompletedTask,(_,_,_)=>Task.CompletedTask);
            var vm=new MainViewModel(network);vm.SelectedAdapter=new(b);
            var p=new Preset {Name="A template",AdapterName="A",AdapterInterfaceGuid="a",Config=Dhcp()};vm.SelectedPreset=p;
            Require(vm.IsCrossAdapter && vm.CrossAdapterText.Contains("B"),"Missing cross-adapter warning.");
            var previews=0;
            vm.ReviewConfiguration=(target,_)=>{previews++;Require(target.InterfaceGuid=="b","Preview did not use selected adapter.");return false;};
            vm.ApplyPresetCommand.Execute(p);await vm.ActiveOperation;
            Require(previews==1 && reads==1 && writes==0,"Applying a template must open exactly one preview; cancelling it must not write.");
        });
        await Check("附加地址校验及模板兼容去重", () =>
        {
            foreach(var addresses in new[]{new[]{"bad"},new[]{"192.0.2.1","192.0.2.1"}})
                Require(!ValidationService.ValidateManualConfig(Dhcp(addresses)).IsValid,"Invalid additional IP accepted.");
            Require(JsonSerializer.Deserialize<NetworkConfig>("{}")!.DhcpStaticAddresses.Count==0,"Old settings lost compatibility.");
            var p=new Preset {AdapterInterfaceGuid="a",Config=Dhcp("192.0.2.1")};
            Require(!PresetService.MatchesConfiguration(p,Dhcp("192.0.2.2"),null,null,"a"),"Different additional IPs deduplicated.");
            return Task.CompletedTask;
        });
        await Check("显示偏好持久化和DPI换算", async () =>
        {
            var path=Path.Combine(Path.GetTempPath(),"display-"+Guid.NewGuid()+".json");
            try {
                var p=new PreferencesService(path);await p.SaveAsync(new() {FollowSystemDpi=false,UiScale=1.3,FontScale=1.2});var read=new PreferencesService(path);read.Load();
                Require(read.Current.UiScale==1.3 && read.Current.FontScale==1.2 && !read.Current.FollowSystemDpi,"Display settings lost.");
                Require(Math.Abs(UiDisplayService.LayoutScale(read.Current,1.5)-1.3/1.5)<.001,"Manual scale double-applies system DPI.");
                Require(UiDisplayService.LayoutScale(new(),2)==1,"Default does not follow system DPI.");
                Require(PreferencesService.Normalize(new(){UiScale=99,FontScale=.1}).UiScale==1.75,"Unsafe scale not bounded.");
            } finally {if(File.Exists(path))File.Delete(path);}
        });
        await Check("只读模式阻止所有网络写入入口", async () =>
        {
            var calls=0;
            var network=new NetworkService(_=>{calls++;return Task.FromResult<IReadOnlyList<AdapterSnapshot>>([]);},
                (_,_,_,_)=>{calls++;return Task.CompletedTask;},(_,_)=>Task.CompletedTask,(_,_,_)=>Task.CompletedTask,allowWrites:false);
            var vm=new MainViewModel(network) { SelectedAdapter=new(new AdapterSnapshot()) };
            Require(!vm.CanConfigure && !vm.ApplyPresetCommand.CanExecute(new Preset()),"Read-only commands enabled.");
            Require(!(await network.ApplyReviewedAsync(new(),Dhcp())).Succeeded,"Reviewed write permitted.");
            Require(!(await network.ApplyAsync(new(),Dhcp(),OperationSource.Manual)).Succeeded,"Direct write permitted.");
            Require(!(await network.SetAdapterEnabledAsync(new(),true)).Succeeded && calls==0,"Read-only mode reached OS transport.");
        });
    }
}
