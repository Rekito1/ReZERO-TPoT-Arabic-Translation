using System;
using System.IO;
using System.IO.Compression;
using System.Drawing;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Web.Script.Serialization;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Reflection;
using Microsoft.Win32;
using System.Text.RegularExpressions;

[assembly: AssemblyTitle("Re:ZERO Arabic Localization")]
[assembly: AssemblyProduct("Re:ZERO Arabic Localization")]
[assembly: AssemblyVersion("2.2.0.0")]
[assembly: AssemblyFileVersion("2.2.0.0")]

class Asset { public string name,entry,before,after; public long offset; public int size,index; }
class Plan { public string archiveHash,exeBefore,exeAfter; public int @base; public Asset[] assets,external; }
class ExternalBackup { public string name,before,after,backup; }
class Journal { public long length; public string archiveBefore,exeBefore,archiveAfter,exeAfter; public bool complete; public ExternalBackup[] external; }
class Engine {
 public static JavaScriptSerializer Json=new JavaScriptSerializer();
 public static ZipArchive Zip=new ZipArchive(Assembly.GetExecutingAssembly().GetManifestResourceStream("payload.zip"));
 public static byte[] Data(string n) { using(var s=Zip.GetEntry(n).Open())using(var m=new MemoryStream()){s.CopyTo(m);return m.ToArray();} }
 public static Plan P=Json.Deserialize<Plan>(System.Text.Encoding.UTF8.GetString(Data("plan.json")));
 public static string Hash(byte[] b){using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(b)).Replace("-","").ToLowerInvariant();}
 public static string HashFile(string p){using(var h=SHA256.Create())using(var s=File.OpenRead(p))return BitConverter.ToString(h.ComputeHash(s)).Replace("-","").ToLowerInvariant();}
 public static byte[] Patch(byte[] source,string entry){
  using(var s=new GZipStream(new MemoryStream(Data(entry)),CompressionMode.Decompress))using(var r=new BinaryReader(s))using(var o=new MemoryStream()){
   if(new string(r.ReadChars(4))!="RZP1")throw new Exception("Invalid patch");int length=r.ReadInt32();
   while(o.Length<length){byte type=r.ReadByte();if(type==0){int pos=r.ReadInt32(),n=r.ReadInt32();if(pos<0||n<0||pos>source.Length-n)throw new Exception("Invalid source range");o.Write(source,pos,n);}else if(type==1){int n=r.ReadInt32();if(n<0||n>length-o.Length)throw new Exception("Invalid literal");byte[] b=r.ReadBytes(n);if(b.Length!=n)throw new Exception("Truncated patch");o.Write(b,0,n);}else throw new Exception("Invalid opcode");}
   if(o.Length!=length)throw new Exception("Invalid patch length");return o.ToArray();
  }
 }
 public static void Guard(string dir){if(Process.GetProcessesByName("ReZERO-TPT").Length>0)throw new Exception("أغلق اللعبة أولًا ثم حاول مجددًا.");if(!File.Exists(Path.Combine(dir,"ReZERO-TPT.exe"))||!File.Exists(Path.Combine(dir,"00000000.cfsi")))throw new Exception("اختر مجلد اللعبة الذي يحتوي ReZERO-TPT.exe.");}
 public static void Install(string dir,Action<string> status){
  Guard(dir);string arc=Path.Combine(dir,"00000000.cfsi"),exe=Path.Combine(dir,"ReZERO-TPT.exe"),bak=Path.Combine(dir,".rezero-arabic-backup");
  if(Directory.Exists(bak))throw new Exception("توجد نسخة استرجاع. استخدم إزالة التعريب أولًا.");status("التحقق من توافق ملفات اللعبة…");
  if(HashFile(arc)!=P.archiveHash||HashFile(exe)!=P.exeBefore)throw new Exception("هذه الملفات غير متوافقة أو معدّلة سابقًا. يلزم إصدار أصلي مطابق؛ لم تتغير أي ملفات.");
  var payloads=new List<byte[]>();using(var f=File.OpenRead(arc))foreach(var a in P.assets){f.Position=a.offset;byte[] b=new byte[a.size];int got=0;while(got<b.Length){int n=f.Read(b,got,b.Length-got);if(n==0)throw new EndOfStreamException();got+=n;}if(Hash(b)!=a.before)throw new Exception("Source mismatch");b=Patch(b,a.entry);if(Hash(b)!=a.after)throw new Exception("Patch mismatch");payloads.Add(b);}
  byte[] newExe=Patch(File.ReadAllBytes(exe),"exe.delta");if(Hash(newExe)!=P.exeAfter)throw new Exception("Executable mismatch");
  var extraPayloads=new List<byte[]>();var extraBackups=new List<ExternalBackup>();
  if(P.external!=null)for(int i=0;i<P.external.Length;i++){var a=P.external[i];string file=Path.Combine(dir,a.name.Replace('/',Path.DirectorySeparatorChar));if(!File.Exists(file)||HashFile(file)!=a.before)throw new Exception("فيديو الانترو مختلف عن النسخة المدعومة؛ لم تتغير أي ملفات.");byte[] data=Patch(File.ReadAllBytes(file),a.entry);if(Hash(data)!=a.after)throw new Exception("Video patch mismatch");extraPayloads.Add(data);extraBackups.Add(new ExternalBackup{name=a.name,before=a.before,after=a.after,backup="external-"+i+".bin"});}
  status("إنشاء نسخة الاسترجاع…");Directory.CreateDirectory(bak);File.Copy(exe,Path.Combine(bak,"original.exe"));
  using(var f=File.OpenRead(arc)){byte[] h=new BinaryReader(f).ReadBytes(P.@base);if(h.Length!=P.@base)throw new EndOfStreamException();File.WriteAllBytes(Path.Combine(bak,"header.bin"),h);}
  foreach(var x in extraBackups)File.Copy(Path.Combine(dir,x.name.Replace('/',Path.DirectorySeparatorChar)),Path.Combine(bak,x.backup));
  var j=new Journal{length=new FileInfo(arc).Length,archiveBefore=P.archiveHash,exeBefore=P.exeBefore,external=extraBackups.ToArray()};File.WriteAllText(Path.Combine(bak,"state.json"),Json.Serialize(j));
  try{
   status("تركيب الحوارات والقوائم والخطوط…");using(var f=new FileStream(arc,FileMode.Open,FileAccess.ReadWrite))using(var w=new BinaryWriter(f))for(int i=0;i<P.assets.Length;i++){f.Position=f.Length;while(f.Position%16!=0)w.Write((byte)0);long offset=f.Position;w.Write(payloads[i]);f.Flush(true);f.Position=P.assets[i].index;w.Write(checked((uint)((offset-P.@base)/16)));w.Write(payloads[i].Length);f.Flush(true);}
   File.WriteAllBytes(exe,newExe);status("تركيب الانترو المترجم…");for(int i=0;i<extraBackups.Count;i++){var x=extraBackups[i];string file=Path.Combine(dir,x.name.Replace('/',Path.DirectorySeparatorChar));File.WriteAllBytes(file,extraPayloads[i]);if(HashFile(file)!=x.after)throw new Exception("Video verification failed");}status("التحقق من التثبيت…");using(var check=File.OpenRead(arc))using(var reader=new BinaryReader(check)){foreach(var a in P.assets){check.Position=a.index;long offset=(long)reader.ReadUInt32()*16+P.@base;int count=reader.ReadInt32();check.Position=offset;if(Hash(reader.ReadBytes(count))!=a.after)throw new Exception("فشل التحقق من الملف المركب: "+a.name);}}j.archiveAfter=HashFile(arc);j.exeAfter=HashFile(exe);if(j.exeAfter!=P.exeAfter)throw new Exception("Verification failed");j.complete=true;File.WriteAllText(Path.Combine(bak,"state.json"),Json.Serialize(j));
  }catch{Restore(dir,status,true);throw;}
 }
 public static void Restore(string dir,Action<string> status,bool recovery=false){
  Guard(dir);string arc=Path.Combine(dir,"00000000.cfsi"),exe=Path.Combine(dir,"ReZERO-TPT.exe"),bak=Path.Combine(dir,".rezero-arabic-backup"),state=Path.Combine(bak,"state.json");
  if(!File.Exists(state))throw new Exception("لا توجد نسخة استرجاع أنشأها هذا المثبّت.");var j=Json.Deserialize<Journal>(File.ReadAllText(state));
  if(j.archiveBefore!=P.archiveHash||j.exeBefore!=P.exeBefore||HashFile(Path.Combine(bak,"original.exe"))!=P.exeBefore)throw new Exception("نسخة الاسترجاع غير مطابقة.");
  status("فحص ملفات الاسترجاع…");if(j.complete&&!recovery&&(HashFile(arc)!=j.archiveAfter||HashFile(exe)!=j.exeAfter))throw new Exception("تغيرت ملفات اللعبة بعد التثبيت؛ أوقفت الاسترجاع لحماية التعديلات الجديدة.");
  if(j.external!=null)for(int i=0;i<j.external.Length;i++){var x=j.external[i];if(P.external==null||i>=P.external.Length||x.name!=P.external[i].name||x.backup!="external-"+i+".bin"||x.before!=P.external[i].before)throw new Exception("Invalid movie backup record");string source=Path.Combine(bak,x.backup),file=Path.Combine(dir,x.name.Replace('/',Path.DirectorySeparatorChar));if(!File.Exists(source)||HashFile(source)!=x.before)throw new Exception("نسخة استرجاع الانترو غير مطابقة.");if(j.complete&&!recovery&&(!File.Exists(file)||HashFile(file)!=x.after))throw new Exception("تغير فيديو الانترو بعد التثبيت؛ أوقفت الاسترجاع لحماية الملف.");}
  byte[] header=File.ReadAllBytes(Path.Combine(bak,"header.bin"));if(header.Length!=P.@base)throw new Exception("Invalid backup header");
  using(var f=new FileStream(arc,FileMode.Open,FileAccess.Write)){f.Write(header,0,header.Length);f.SetLength(j.length);f.Flush(true);}File.Copy(Path.Combine(bak,"original.exe"),exe,true);
  if(j.external!=null)foreach(var x in j.external){string file=Path.Combine(dir,x.name.Replace('/',Path.DirectorySeparatorChar));File.Copy(Path.Combine(bak,x.backup),file,true);if(HashFile(file)!=x.before)throw new Exception("فشل التحقق من استرجاع الانترو.");}
  if(HashFile(arc)!=j.archiveBefore||HashFile(exe)!=j.exeBefore)throw new Exception("فشل فحص الاسترجاع؛ احتفظ بالنسخة الاحتياطية.");
  if(j.external!=null)foreach(var x in j.external)File.Delete(Path.Combine(bak,x.backup));foreach(string name in new[]{"header.bin","original.exe","state.json"})File.Delete(Path.Combine(bak,name));Directory.Delete(bak);status("تمت استعادة الملفات الأصلية.");
 }
}
class MainForm:Form{
 TextBox path=new TextBox();Label status=new Label();Button install=new Button(),restore=new Button(),browse=new Button();ProgressBar progress=new ProgressBar();
 Image artwork=Image.FromStream(new MemoryStream(Engine.Data("trio-nintendo.jpg")));
 protected override void OnPaintBackground(PaintEventArgs e){
  var g=e.Graphics;g.InterpolationMode=System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
  g.DrawImage(artwork,new Rectangle(0,0,1280,640));
  using(var fade=new System.Drawing.Drawing2D.LinearGradientBrush(new Rectangle(0,350,1040,290),Color.FromArgb(0,248,245,253),Color.FromArgb(242,248,245,253),90f))g.FillRectangle(fade,0,350,1040,290);
  using(var glass=new SolidBrush(Color.FromArgb(190,249,247,253)))g.FillRectangle(glass,28,400,984,218);
  using(var edge=new Pen(Color.FromArgb(180,255,255,255)))g.DrawRectangle(edge,28,400,984,218);
  using(var p=new Pen(Color.FromArgb(130,190,172,214)))g.DrawLine(p,617,425,617,573);
  using(var p=new Pen(Color.FromArgb(207,195,225)))g.DrawRectangle(p,0,0,1039,639);
  g.TextRenderingHint=System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
  using(var font=new Font("Segoe UI",24,FontStyle.Bold))using(var brush=new SolidBrush(Color.FromArgb(53,39,75)))using(var format=new StringFormat{Alignment=StringAlignment.Near,LineAlignment=StringAlignment.Center,FormatFlags=StringFormatFlags.DirectionRightToLeft})g.DrawString("الترجمة العربية",font,brush,new RectangleF(652,402,340,66),format);
 }
 [System.Runtime.InteropServices.DllImport("user32.dll")]static extern bool ReleaseCapture();
 [System.Runtime.InteropServices.DllImport("user32.dll")]static extern IntPtr SendMessage(IntPtr h,int m,IntPtr w,IntPtr l);
 protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);if(e.Button==MouseButtons.Left&&e.Y<130){ReleaseCapture();SendMessage(Handle,0xA1,(IntPtr)2,IntPtr.Zero);}}
 public MainForm(){Text="Re:ZERO • التعريب العربي";ClientSize=new Size(1040,640);FormBorderStyle=FormBorderStyle.None;MaximizeBox=false;StartPosition=FormStartPosition.CenterScreen;DoubleBuffered=true;ForeColor=Color.FromArgb(53,39,75);Font=new Font("Segoe UI",11);
 var close=new Button{Text="×",Bounds=new Rectangle(992,14,32,30),FlatStyle=FlatStyle.Flat,BackColor=Color.FromArgb(70,52,91),ForeColor=Color.White,Font=new Font("Segoe UI",16)};close.FlatAppearance.BorderSize=0;close.Click+=(sender,args)=>Close();Controls.Add(close);
 var minimize=new Button{Text="−",Bounds=new Rectangle(956,14,32,30),FlatStyle=FlatStyle.Flat,BackColor=Color.FromArgb(70,52,91),ForeColor=Color.White};minimize.FlatAppearance.BorderSize=0;minimize.Click+=(sender,args)=>WindowState=FormWindowState.Minimized;Controls.Add(minimize);
 AddLabel("الحوارات والقوائم والانترو",470,11,FontStyle.Regular);var pl=new Label{Text="مجلد اللعبة",Bounds=new Rectangle(56,424,520,30),TextAlign=ContentAlignment.MiddleRight,Font=new Font("Segoe UI",11,FontStyle.Bold),BackColor=Color.Transparent};Controls.Add(pl);
 path.SetBounds(140,468,436,32);path.BackColor=Color.White;path.ForeColor=Color.FromArgb(53,39,75);path.BorderStyle=BorderStyle.FixedSingle;path.Text=FindGame();Controls.Add(path);
 browse.Text="تحديد";browse.SetBounds(56,466,74,34);StyleButton(browse,false);browse.Click+=(s,e)=>{using(var d=new FolderBrowserDialog()){d.Description="اختر مجلد ReZERO-TPT";if(d.ShowDialog()==DialogResult.OK)path.Text=d.SelectedPath;}};Controls.Add(browse);
 var note=new Label{Text="نسخة احتياطية تلقائية قبل التثبيت",Bounds=new Rectangle(56,508,520,30),TextAlign=ContentAlignment.MiddleRight,BackColor=Color.Transparent,Font=new Font("Segoe UI",10)};Controls.Add(note);
 install.Text="تثبيت التعريب";install.SetBounds(652,523,340,48);StyleButton(install,true);install.Click+=(s,e)=>Run(false);Controls.Add(install);
 restore.Text="إزالة التعريب واستعادة الأصل";restore.SetBounds(56,545,520,32);StyleButton(restore,false);restore.Click+=(s,e)=>Run(true);Controls.Add(restore);
 progress.SetBounds(56,586,936,4);progress.Style=ProgressBarStyle.Marquee;progress.Visible=false;Controls.Add(progress);
 status.SetBounds(56,594,936,30);status.Text="بعد التثبيت اختر English من لغة النص لعرض العربية.";status.Font=new Font("Segoe UI",10);status.TextAlign=ContentAlignment.TopRight;status.RightToLeft=RightToLeft.Yes;status.BackColor=Color.Transparent;Controls.Add(status);
 var credit=new Label{Text="التعريب مقدم لكم من Re_kito",Bounds=new Rectangle(56,617,936,20),BackColor=Color.Transparent,ForeColor=Color.FromArgb(91,73,112),Font=new Font("Segoe UI",9),TextAlign=ContentAlignment.MiddleCenter,RightToLeft=RightToLeft.Yes};Controls.Add(credit);
 }
 void StyleButton(Button b,bool primary){b.FlatStyle=FlatStyle.Flat;b.FlatAppearance.BorderColor=Color.FromArgb(208,195,224);b.FlatAppearance.BorderSize=primary?0:1;b.BackColor=primary?Color.FromArgb(113,76,161):Color.FromArgb(244,239,250);b.ForeColor=primary?Color.White:Color.FromArgb(89,58,125);b.Cursor=Cursors.Hand;b.Font=new Font("Segoe UI",11,primary?FontStyle.Bold:FontStyle.Regular);}
 void AddLabel(string text,int y,int size,FontStyle style){var l=new Label{Text=text,Bounds=new Rectangle(652,y,340,size>=20?64:40),TextAlign=ContentAlignment.MiddleRight,BackColor=Color.Transparent,Font=new Font("Segoe UI",size,style)};Controls.Add(l);}
 bool busy;protected override void OnFormClosing(FormClosingEventArgs e){if(busy)e.Cancel=true;base.OnFormClosing(e);}
 async void Run(bool undo){string dir=path.Text.Trim();busy=true;install.Enabled=restore.Enabled=browse.Enabled=path.Enabled=false;progress.Visible=true;try{await Task.Run(()=>{Action<string> report=t=>BeginInvoke((Action)(()=>status.Text=t));if(undo)Engine.Restore(dir,report);else Engine.Install(dir,report);});status.Text=undo?"تمت إزالة التعريب واستعادة الأصل.":"تم التثبيت بنجاح. اختر English من لغة النص لعرض العربية.";}catch(Exception ex){status.Text=ex.Message;MessageBox.Show(this,ex.Message,"Re:ZERO",MessageBoxButtons.OK,MessageBoxIcon.Information);}finally{busy=false;install.Enabled=restore.Enabled=browse.Enabled=path.Enabled=true;progress.Visible=false;}}
 static string FindGame(){var roots=new List<string>();string steam=null;try{steam=Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam","SteamPath",null) as string;}catch(System.Security.SecurityException){}if(steam!=null){roots.Add(steam);string vdf=Path.Combine(steam,"steamapps","libraryfolders.vdf");if(File.Exists(vdf))foreach(Match m in Regex.Matches(File.ReadAllText(vdf),"\"path\"\\s+\"([^\"]+)\""))roots.Add(m.Groups[1].Value.Replace("\\\\","\\"));}roots.Add(@"C:\Program Files (x86)\Steam");foreach(string root in roots){string dir=Path.Combine(root,"steamapps","common","ReZERO-TPT");if(File.Exists(Path.Combine(dir,"ReZERO-TPT.exe")))return dir;}return "";}
 [STAThread]static int Main(string[] args){try{if(args.Length==2&&args[0]=="--selftest"){Engine.Install(args[1],Console.WriteLine);Engine.Restore(args[1],Console.WriteLine);File.WriteAllText(Path.Combine(args[1],"selftest-ok.txt"),"Install and uninstall completed; original SHA256 hashes verified.");return 0;}Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);Application.Run(new MainForm());return 0;}catch(Exception e){File.WriteAllText(Path.Combine(Path.GetTempPath(),"rezero-installer-error.txt"),e.ToString());return 1;}}
}
