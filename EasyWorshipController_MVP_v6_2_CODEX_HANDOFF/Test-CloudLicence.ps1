$ErrorActionPreference='Stop'
$source=Get-Content (Join-Path $PSScriptRoot 'GazelyCloud.cs') -Raw -Encoding UTF8
$test=@'
namespace EasyWorshipControllerMvp {
 public static class CloudLicenceTest {
  static string B64(byte[] b){return Convert.ToBase64String(b).TrimEnd('=').Replace('+','-').Replace('/','_');}
  static string Token(RSACryptoServiceProvider rsa,string payload){
   string encoded=B64(Encoding.UTF8.GetBytes(payload));
   return encoded+"."+B64(rsa.SignData(Encoding.UTF8.GetBytes(encoded),CryptoConfig.MapNameToOID("SHA256")));
  }
  static void Reject(Action action,string label){try{action();throw new Exception("Accepted "+label);}catch(InvalidOperationException){ }catch(FormatException){ }}
  public static void Run(){
   GazelyCloudAccess.PathOverride=null;
   using(var rsa=new RSACryptoServiceProvider(2048)){
    RSAParameters key=rsa.ExportParameters(false);
    string modulus=Convert.ToBase64String(key.Modulus),exponent=Convert.ToBase64String(key.Exponent);
    string installation=Guid.NewGuid().ToString();
    DateTime now=DateTime.UtcNow;
    var payload=new System.Collections.Generic.Dictionary<string,object>{{"v",1},{"iss","Gazely"},
      {"church_id",Guid.NewGuid().ToString()},{"device_id",Guid.NewGuid().ToString()},
      {"installation_id",installation},{"plan","free"},{"access_mode","billing"},
      {"entitlements",new System.Collections.Generic.Dictionary<string,object>{{"auto_prepare",false},{"automatic_live",false}}},
      {"allowance_seconds",3600},{"accepted_seconds",120},
      {"period_start",now.AddDays(-1).ToString("o")},{"period_end",now.AddDays(29).ToString("o")},
      {"issued_at",now.AddMinutes(-1).ToString("o")},{"expires_at",now.AddDays(6).ToString("o")}};
    string valid=Token(rsa,new JavaScriptSerializer().Serialize(payload));
    GazelyLease lease=GazelyLeaseVerifier.Verify(valid,modulus,exponent,installation,now);
    if(lease.Plan!="free" || lease.AcceptedSeconds!=120 || lease.AutoPrepare)throw new Exception("Valid lease parsed incorrectly");
    Reject(delegate{GazelyLeaseVerifier.Verify(valid,modulus,exponent,Guid.NewGuid().ToString(),now);},"other device");
    Reject(delegate{GazelyLeaseVerifier.Verify(valid,modulus,exponent,installation,now.AddDays(8));},"expired lease");
    string altered=valid.Substring(0,5)+(valid[5]=='A'?'B':'A')+valid.Substring(6);
    Reject(delegate{GazelyLeaseVerifier.Verify(altered,modulus,exponent,installation,now);},"altered lease");
    payload["expires_at"]=now.AddDays(9).ToString("o");
    string longFree=Token(rsa,new JavaScriptSerializer().Serialize(payload));
    Reject(delegate{GazelyLeaseVerifier.Verify(longFree,modulus,exponent,installation,now);},"overlong Free lease");
    payload["expires_at"]=now.AddDays(6).ToString("o");
    payload["access_mode"]="launch_free";
    payload["allowance_seconds"]=null;
    payload["entitlements"]=new System.Collections.Generic.Dictionary<string,object>{{"auto_prepare",true},{"automatic_live",true}};
    var launch=GazelyLeaseVerifier.Verify(Token(rsa,new JavaScriptSerializer().Serialize(payload)),modulus,exponent,installation,now);
    if(launch.AccessMode!="launch_free" || launch.AllowanceSeconds!=null || !launch.AutoPrepare || !launch.AutomaticLive)
      throw new Exception("Full free access lease parsed incorrectly");
    payload["access_mode"]="billing";
    Reject(delegate{GazelyLeaseVerifier.Verify(Token(rsa,new JavaScriptSerializer().Serialize(payload)),modulus,exponent,installation,now);},"unlimited billed Free lease");
    Console.WriteLine("PASS: signed lease, device binding, expiry, tampering, launch access and Free window.");
   }
  }
 }
}
'@
$types=Add-Type -TypeDefinition ($source+[Environment]::NewLine+$test) -ReferencedAssemblies @('System.dll','System.Core.dll','System.Security.dll','System.Web.Extensions.dll') -PassThru
$type=$types | Where-Object FullName -eq 'EasyWorshipControllerMvp.CloudLicenceTest' | Select-Object -First 1
$type.GetMethod('Run').Invoke($null,@())
