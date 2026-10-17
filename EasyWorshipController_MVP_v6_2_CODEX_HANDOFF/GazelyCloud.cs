using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace EasyWorshipControllerMvp
{
    internal static class GazelyCloudBuild
    {
        // Replaced in memory by Build-EasyWorshipController.ps1 for a cloud release.
        internal const string Url="__GAZELY_SUPABASE_URL__";
        internal const string PublishableKey="__GAZELY_SUPABASE_PUBLISHABLE_KEY__";
        internal const string RsaModulus="__GAZELY_RSA_MODULUS__";
        internal const string RsaExponent="__GAZELY_RSA_EXPONENT__";
        internal const string PortalUrl="__GAZELY_PORTAL_URL__";
#if GAZELY_CLOUD_RELEASE
        internal static readonly bool Required=true;
#else
        internal static readonly bool Required=false;
#endif
#if GAZELY_EMAIL_ENTRY
        internal static readonly bool EmailEntry=true;
#else
        internal static readonly bool EmailEntry=false;
#endif
#if GAZELY_FREE_ACCESS
        internal static readonly bool FreeAccess=true;
#else
        internal static readonly bool FreeAccess=false;
#endif
        internal static bool LoginRequired { get { return Required || EmailEntry; } }
        internal static bool EndpointConfigured { get { return Url.StartsWith("https://") && !Url.Contains("__GAZELY_") && PublishableKey.Length>16; } }
        internal static bool Configured { get { return Url.StartsWith("https://") && !Url.Contains("__GAZELY_") &&
            PublishableKey.Length>16 && RsaModulus.Length>100 && RsaExponent.Length>0; } }
    }

    internal sealed class GazelyLease
    {
        internal string ChurchId,DeviceId,InstallationId,Plan,AccessMode,PeriodStart,PeriodEnd;
        internal DateTime ExpiresUtc;
        internal int? AllowanceSeconds;
        internal int AcceptedSeconds;
        internal bool AutoPrepare,AutomaticLive;
    }

    internal static class GazelyLeaseVerifier
    {
        private static byte[] FromUrl(string text)
        {
            text=text.Replace('-','+').Replace('_','/');
            switch(text.Length%4){case 2:text+="==";break;case 3:text+="=";break;case 1:throw new FormatException("Invalid licence encoding");}
            return Convert.FromBase64String(text);
        }
        internal static GazelyLease Verify(string token,string modulus,string exponent,string installationId,DateTime nowUtc)
        {
            if(String.IsNullOrEmpty(token) || token.Length>8192)throw new InvalidOperationException("Missing licence");
            string[] parts=token.Split('.');if(parts.Length!=2)throw new InvalidOperationException("Invalid licence");
            using(RSACryptoServiceProvider rsa=new RSACryptoServiceProvider())
            {
                rsa.ImportParameters(new RSAParameters{Modulus=Convert.FromBase64String(modulus),Exponent=Convert.FromBase64String(exponent)});
                if(!rsa.VerifyData(Encoding.UTF8.GetBytes(parts[0]),CryptoConfig.MapNameToOID("SHA256"),FromUrl(parts[1])))
                    throw new InvalidOperationException("Licence signature is invalid");
            }
            var data=new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(Encoding.UTF8.GetString(FromUrl(parts[0])));
            if(Convert.ToInt32(data["v"])!=1 || Convert.ToString(data["iss"])!="Gazely" ||
               Convert.ToString(data["installation_id"])!=installationId)throw new InvalidOperationException("Licence is for another installation");
            DateTime expires=DateTime.Parse(Convert.ToString(data["expires_at"]),null,System.Globalization.DateTimeStyles.AdjustToUniversal);
            DateTime issued=DateTime.Parse(Convert.ToString(data["issued_at"]),null,System.Globalization.DateTimeStyles.AdjustToUniversal);
            if(expires<=nowUtc || issued>nowUtc.AddMinutes(5) || expires>issued.AddDays(30).AddMinutes(1))
                throw new InvalidOperationException("Licence has expired or has invalid dates");
            var entitlements=(Dictionary<string,object>)data["entitlements"];
            string plan=Convert.ToString(data["plan"]);
            object modeValue;string accessMode=data.TryGetValue("access_mode",out modeValue)?Convert.ToString(modeValue):"billing";
            if(plan!="free" && plan!="pro")throw new InvalidOperationException("Invalid licence plan");
            if(accessMode!="billing" && accessMode!="launch_free")throw new InvalidOperationException("Invalid access mode");
            if(accessMode=="launch_free" && plan!="free")throw new InvalidOperationException("Invalid free launch plan");
            if(plan=="free" && expires>issued.AddDays(7).AddMinutes(1))throw new InvalidOperationException("Free lease is too long");
            int? allowance=data["allowance_seconds"]==null?(int?)null:Convert.ToInt32(data["allowance_seconds"]);
            if(plan=="free" && (accessMode=="launch_free" ? allowance!=null : allowance!=3600))
                throw new InvalidOperationException("Invalid Free allowance");
            if(plan=="pro" && allowance!=null)throw new InvalidOperationException("Invalid Pro allowance");
            if(accessMode=="launch_free" && (!Convert.ToBoolean(entitlements["auto_prepare"]) ||
                !Convert.ToBoolean(entitlements["automatic_live"])))throw new InvalidOperationException("Incomplete free launch access");
            return new GazelyLease{ChurchId=Convert.ToString(data["church_id"]),DeviceId=Convert.ToString(data["device_id"]),
                InstallationId=installationId,Plan=plan,AccessMode=accessMode,PeriodStart=Convert.ToString(data["period_start"]),
                PeriodEnd=Convert.ToString(data["period_end"]),ExpiresUtc=expires,AllowanceSeconds=allowance,
                AcceptedSeconds=Convert.ToInt32(data["accepted_seconds"]),
                AutoPrepare=Convert.ToBoolean(entitlements["auto_prepare"]),AutomaticLive=Convert.ToBoolean(entitlements["automatic_live"])};
        }
    }

    internal sealed class GazelyCloudAccess
    {
        internal static string PathOverride {get;set;}
        private readonly object gate=new object();
        private readonly string folder;
        private readonly JavaScriptSerializer serializer=new JavaScriptSerializer();
        private Dictionary<string,object> state;
        private GazelyLease lease;
        private long lastTicks;
        private bool listening;
        private int unsentSeconds;
        internal string InstallationId { get { lock(gate)return Convert.ToString(state["installation_id"]); } }
        internal string Plan { get { lock(gate)return lease==null?"Not activated":lease.Plan; } }
        internal string Email { get { lock(gate)return Value(GazelyCloudBuild.EmailEntry?"email_entry":"email"); } }
        internal string Status { get { lock(gate)return GazelyCloudBuild.FreeAccess?"Free offline access":GazelyCloudBuild.EmailEntry?
            (HasLocalEmail()?"Email entered · ready to use Gazely":"Enter your email to continue"):
            GazelyCloudBuild.Configured?(lease==null?"Sign in and activate this computer":
            (lease.AccessMode=="launch_free"?"Free · full access":lease.Plan)+" · valid until "+lease.ExpiresUtc.ToLocalTime().ToString("d")):"Cloud account configuration pending"; } }
        internal bool CanAutoPrepare { get { lock(gate)return GazelyCloudBuild.EmailEntry?HasLocalEmail():!GazelyCloudBuild.Required || (lease!=null && lease.AutoPrepare); } }
        internal bool CanAutomaticLive { get { lock(gate)return GazelyCloudBuild.EmailEntry?HasLocalEmail():!GazelyCloudBuild.Required || (lease!=null && lease.AutomaticLive); } }
        internal GazelyCloudAccess()
        {
            folder=PathOverride??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Gazely","account");
            Directory.CreateDirectory(folder);
            string path=Path.Combine(folder,"account.dat");
            try {
                byte[] protectedBytes=File.ReadAllBytes(path);
                byte[] clear=ProtectedData.Unprotect(protectedBytes,null,DataProtectionScope.CurrentUser);
                state=serializer.Deserialize<Dictionary<string,object>>(Encoding.UTF8.GetString(clear));
            } catch { state=new Dictionary<string,object>(); }
            if(!state.ContainsKey("installation_id"))state["installation_id"]=Guid.NewGuid().ToString();
            if(!state.ContainsKey("events"))state["events"]=new object[0];
            if(GazelyCloudBuild.Configured || GazelyCloudBuild.LoginRequired)Save();
            LoadLease();
        }
        private string Value(string key){object value;return state.TryGetValue(key,out value)?Convert.ToString(value):"";}
        private bool HasLocalEmail(){return IsEmail(Value("email_entry"));}
        private static bool IsEmail(string email){
            if(String.IsNullOrWhiteSpace(email) || email.Length>254 || email.IndexOfAny(new[]{' ','\t','\r','\n'})>=0)return false;
            int at=email.IndexOf('@');return at>0 && at==email.LastIndexOf('@') &&
                at<email.Length-3 && email.IndexOf('.',at+2)>at+1 && !email.EndsWith(".",StringComparison.Ordinal);
        }
        internal Task EnterEmail(string input){
            string email=(input??"").Trim();
            if(!IsEmail(email))throw new InvalidOperationException("Enter a valid email address to continue.");
            return Task.Run(delegate{
                var result=Http("/functions/v1/gazely-email-entry",new Dictionary<string,object>{
                    {"installation_id",InstallationId},{"email",email}},null);
                object recorded;if(!result.TryGetValue("recorded",out recorded) || !Convert.ToBoolean(recorded))
                    throw new InvalidOperationException("Email was not recorded. Please retry.");
                lock(gate){state["email_entry"]=email;Save();}
            });
        }
        private void Save()
        {
            lock(gate){
                byte[] clear=Encoding.UTF8.GetBytes(serializer.Serialize(state));
                byte[] protectedBytes=ProtectedData.Protect(clear,null,DataProtectionScope.CurrentUser);
                string path=Path.Combine(folder,"account.dat"),temp=path+".new";
                File.WriteAllBytes(temp,protectedBytes);
                if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);
            }
        }
        private void LoadLease()
        {
            lease=null;
            if(!GazelyCloudBuild.Configured)return;
            try{
                lease=GazelyLeaseVerifier.Verify(Value("licence"),GazelyCloudBuild.RsaModulus,
                    GazelyCloudBuild.RsaExponent,InstallationId,DateTime.UtcNow);
                if(Value("accepted_period")==lease.PeriodStart){int accepted;
                    if(Int32.TryParse(Value("accepted_floor"),out accepted))lease.AcceptedSeconds=Math.Max(lease.AcceptedSeconds,accepted);}
            }catch{lease=null;}
        }
        internal bool CanStart(out string reason)
        {
            reason=null;
            if(GazelyCloudBuild.EmailEntry){
                lock(gate){if(HasLocalEmail())return true;}
                reason="Enter your email on the Account page before using Gazely.";
                return false;
            }
            if(!GazelyCloudBuild.Required)return true;
            lock(gate){
                LoadLease();
                if(lease==null){reason="Sign in and activate this computer, or refresh its licence before listening.";return false;}
                DateTime previous;
                if(DateTime.TryParse(Value("last_utc"),out previous) && DateTime.UtcNow.AddMinutes(5)<previous.ToUniversalTime()){
                    reason="The PC clock moved backwards. Connect to refresh your licence.";return false;
                }
                int remaining=lease.AllowanceSeconds.HasValue?lease.AllowanceSeconds.Value-lease.AcceptedSeconds-PendingSeconds()-unsentSeconds:int.MaxValue;
                if(remaining<=0){reason="Free listening time is used for this period. Upgrade or wait for the reset date.";return false;}
                return true;
            }
        }
        private List<Dictionary<string,object>> Events()
        {
            var result=new List<Dictionary<string,object>>();
            object raw=state["events"];
            var array=raw as System.Collections.ArrayList;
            if(array!=null)foreach(object item in array)result.Add((Dictionary<string,object>)item);
            var typed=raw as object[];
            if(typed!=null)foreach(object item in typed)result.Add((Dictionary<string,object>)item);
            return result;
        }
        private int PendingSeconds(){int total=0;foreach(var e in Events())total+=Convert.ToInt32(e["seconds"]);return total;}
        internal void Begin(){lock(gate){listening=true;lastTicks=System.Diagnostics.Stopwatch.GetTimestamp();unsentSeconds=0;}}
        internal bool Checkpoint()
        {
            lock(gate){
                if(!listening)return true;
                long now=System.Diagnostics.Stopwatch.GetTimestamp();
                long elapsed=now-lastTicks;
                int whole=(int)(elapsed/System.Diagnostics.Stopwatch.Frequency);
                if(whole<1)return true;
                lastTicks+=(long)whole*System.Diagnostics.Stopwatch.Frequency;
                unsentSeconds+=whole;
                var events=Events();
                bool wrote=false;
                while(unsentSeconds>=15){events.Add(new Dictionary<string,object>{{"id",Guid.NewGuid().ToString()},{"seconds",15}});unsentSeconds-=15;wrote=true;}
                if(wrote){state["events"]=events.ToArray();state["last_utc"]=DateTime.UtcNow.ToString("o");Save();}
                return !GazelyCloudBuild.Required || lease==null || !lease.AllowanceSeconds.HasValue ||
                    lease.AcceptedSeconds+PendingSeconds()+unsentSeconds<lease.AllowanceSeconds.Value;
            }
        }
        internal void End()
        {
            lock(gate){
                Checkpoint();listening=false;
                var events=Events();
                while(unsentSeconds>0){int seconds=Math.Min(15,unsentSeconds);
                    events.Add(new Dictionary<string,object>{{"id",Guid.NewGuid().ToString()},{"seconds",seconds}});unsentSeconds-=seconds;}
                state["events"]=events.ToArray();Save();
            }
        }
        private Dictionary<string,object> Http(string path,object body,string bearer)
        {
            if(!GazelyCloudBuild.EndpointConfigured)throw new InvalidOperationException("Email service configuration pending");
            var request=(HttpWebRequest)WebRequest.Create(GazelyCloudBuild.Url+path);
            request.Method="POST";request.ContentType="application/json";request.Timeout=12000;
            request.Headers["apikey"]=GazelyCloudBuild.PublishableKey;
            if(!String.IsNullOrEmpty(bearer))request.Headers[HttpRequestHeader.Authorization]="Bearer "+bearer;
            byte[] bytes=Encoding.UTF8.GetBytes(serializer.Serialize(body));
            using(Stream stream=request.GetRequestStream())stream.Write(bytes,0,bytes.Length);
            try{using(var response=(HttpWebResponse)request.GetResponse())using(var reader=new StreamReader(response.GetResponseStream()))
                return serializer.Deserialize<Dictionary<string,object>>(reader.ReadToEnd());}
            catch(WebException ex){
                using(var response=ex.Response)if(response!=null)using(var reader=new StreamReader(response.GetResponseStream())){
                    var result=serializer.Deserialize<Dictionary<string,object>>(reader.ReadToEnd());
                    object error;if(result.TryGetValue("error",out error))throw new InvalidOperationException(Convert.ToString(error));
                }
                throw;
            }
        }
        private string AccessToken()
        {
            lock(gate){
                string token=Value("access_token");
                DateTime expires;
                if(!String.IsNullOrEmpty(token) && DateTime.TryParse(Value("token_expires"),out expires) && expires.ToUniversalTime()>DateTime.UtcNow.AddMinutes(1))return token;
                string refresh=Value("refresh_token");if(String.IsNullOrEmpty(refresh))throw new InvalidOperationException("Sign in required");
                var result=Http("/auth/v1/token?grant_type=refresh_token",new Dictionary<string,object>{{"refresh_token",refresh}},null);
                SaveTokens(result);return Value("access_token");
            }
        }
        private void SaveTokens(Dictionary<string,object> result)
        {
            lock(gate){
                state["access_token"]=result["access_token"];state["refresh_token"]=result["refresh_token"];
                state["token_expires"]=DateTime.UtcNow.AddSeconds(Convert.ToInt32(result["expires_in"])).ToString("o");Save();
            }
        }
        private Dictionary<string,object> Api(string action,Dictionary<string,object> fields)
        {
            fields["action"]=action;
            return Http("/functions/v1/gazely-api",fields,AccessToken());
        }
        internal Task SendCode(string email){return Task.Run(delegate{Http("/auth/v1/otp",new Dictionary<string,object>{{"email",email},{"create_user",true}},null);lock(gate){state["email"]=email;Save();}});}
        internal Task VerifyCode(string code){return Task.Run(delegate{
            string email;lock(gate)email=Value("email");
            var result=Http("/auth/v1/verify",new Dictionary<string,object>{{"type","email"},{"email",email},{"token",code}},null);
            SaveTokens(result);
        });}
        internal Task<Dictionary<string,object>> GetStatus(){return Task.Run(delegate{return Api("status",new Dictionary<string,object>());});}
        internal Task CreateChurch(string name,string country){return Task.Run(delegate{
            Api("create_church",new Dictionary<string,object>{{"name",name},{"billing_country",country}});
        });}
        internal Task Activate(string label){return Task.Run(delegate{
            var result=Api("activate_device",new Dictionary<string,object>{{"installation_id",InstallationId},{"label",label}});
            lock(gate){state["licence"]=result["licence"];state["device_id"]=result["device_id"];Save();LoadLease();}
        });}
        internal Task Refresh(){return Task.Run(delegate{
            SyncCore();var result=Api("refresh_licence",new Dictionary<string,object>{{"installation_id",InstallationId}});
            lock(gate){state["licence"]=result["licence"];Save();LoadLease();}
        });}
        internal Task Sync(){return Task.Run(delegate{SyncCore();});}
        private void SyncCore()
        {
            List<Dictionary<string,object>> batch;string device,proof;
            lock(gate){batch=Events();device=Value("device_id");proof=Value("licence");}
            if(batch.Count==0 || String.IsNullOrEmpty(device))return;
            if(batch.Count>120)batch=batch.GetRange(0,120);
            var result=Api("sync_usage",new Dictionary<string,object>{{"device_id",device},{"licence",proof},{"events",batch.ToArray()}});
            lock(gate){
                var sent=new HashSet<string>();foreach(var item in batch)sent.Add(Convert.ToString(item["id"]));
                var remaining=Events();remaining.RemoveAll(delegate(Dictionary<string,object> item){return sent.Contains(Convert.ToString(item["id"]));});
                state["events"]=remaining.ToArray();
                var status=(Dictionary<string,object>)result["status"];
                var usage=(Dictionary<string,object>)status["usage"];
                state["accepted_period"]=Convert.ToString(usage["period_start"]);
                state["accepted_floor"]=Convert.ToString(usage["accepted_seconds"]);
                if(lease!=null && lease.PeriodStart==Convert.ToString(usage["period_start"]))
                    lease.AcceptedSeconds=Math.Max(lease.AcceptedSeconds,Convert.ToInt32(usage["accepted_seconds"]));
                Save();
            }
        }
    }
}
