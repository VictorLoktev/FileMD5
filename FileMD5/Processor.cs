using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Trinet.Core.IO.Ntfs;
using System.Security.Cryptography;
using System.Reflection;

namespace FileMD5
{
	public class Processor
	{
		const string FileCheckedMarkerStreamName = "MD5";
		const string FileDateTimeLastCheckStreamName = "MD5-Last-Check-DateTime";
		const string ExtractExtension = ".MD5";
		const string WrongExtension = ".!!!WrongMD5!!!";
		private static List<string> Work = new List<string>();
		private static string ExtractDir = null;
		private static string ExtractFullPath = null;

		private static string CurTopDir;

		private static bool WorkCheck = false;
		private static bool WorkMD5 = false;
		private static bool WorkOkPlus = false;
		private static bool WorkOkMinus = false;
		private static bool WorkRen = false;
		private static bool WorkSubFolders = false;
		private static bool WorkPause = false;
		private static bool WorkRemove = false;
		private static bool WorkExtract = false;

		public int Run( string[] args )
		{
			Console.WriteLine( "FileMD5 - Контроль целостности файлов.  Используйте ключ -? для справки." );

			bool fault = false;
			fault = ProcessArguments( args );
			if( !WorkMD5 && !WorkRemove ) WorkCheck = true;
			if( !WorkOkMinus ) WorkOkPlus = true;

			if( !fault )
			{
				if( Work.Count == 0 )
				{
					Work.Add( Environment.CurrentDirectory );
					Console.WriteLine( "Директория для обработки не задана, обрабатывается текущая" );
				}
				foreach( string s in Work )
				{
					Process( s );
				}
			}
			if( WorkPause )
			{
				Console.WriteLine( "Нажмите любую кнопку для завершения" );
				Console.ReadKey( true );
			}

			return fault ? 1 : 0;
		}

		private static bool ProcessArguments( string[] args )
		{
			bool fault = false;
			bool catchExtract = false;
			bool catchDo = false;

			foreach( string s in args )
			{
				if( catchExtract )
				{
					ExtractDir = s;
					catchExtract = false;
					bool dirExists = false;
					try
					{
						dirExists = Directory.Exists( s );
						if( !dirExists )
							dirExists = Directory.Exists( Path.GetDirectoryName( s ) );
						if( !dirExists )
							dirExists = Directory.Exists( Path.GetDirectoryName( Path.GetDirectoryName( s ) ) );
						ExtractFullPath = Path.GetFullPath( ExtractDir );
					}
					catch { }
					if( !dirExists )
					{
						Console.WriteLine( "Директория из параметра -extract не существует" );
						fault = true;
					}
				}
				else
				{
					if( s.StartsWith( "-" ) || s.StartsWith( "/" ) )
					{
						switch( s.ToLower() )
						{
						case "-?":
						case "/?":
						case "-h":
						case "/h":
						case "-help":
						case "/help":
							Console.WriteLine( "FileMD5 считает хэшы файлов по алгоритму MD5, сохраняя их отдельными" );
							Console.WriteLine( "потоками в самих файлах (поддерживается только для NTFS)." );
							Console.WriteLine( "При проверке целостности проверяется наличие хэша MD5, сохраненного ранее" );
							Console.WriteLine( "в отдельном потоке в файле; при отсутствии сохраненного ранее хэша MD5" );
							Console.WriteLine( "или при его отличии от текущего рассчитанного хэша выдается ошибка." );
							Console.WriteLine( "" );
							Console.WriteLine( "Вызов программы:" );
							Console.WriteLine( "FileMD5 [parameter1] [...parameterN] [folder or file 1] [...folder or file N]" );
							Console.WriteLine( "folder or file - название папки для обработки всех входящих в нее файлов" );
							Console.WriteLine( "                 или маска файлов, например *.jpg, для обработки" );
							Console.WriteLine( "                 отдельных файлов; может содержать полный или краткий путь" );
							Console.WriteLine( "Ключи:" );
							Console.WriteLine( "  -?      - текущая справка о программе" );
							Console.WriteLine( "  -h      - текущая справка о программе" );
							Console.WriteLine( "  -help   - текущая справка о программе" );
							Console.WriteLine( "  -md5    - считается хэш файла по алгориту MD5; если указан" );
							Console.WriteLine( "            параметр -extract, то хэш пишется в файл в директории его параметра dir" );
							Console.WriteLine( "            (см. параметр -extract), иначе хэш пишется" );
							Console.WriteLine( "            в исходный файл дополнительным потоком (только для NTFS)" );
							Console.WriteLine( "  -check  - считается текущий хэш файла и сравнивается с сохраненным," );
							Console.WriteLine( "            при отличии хэша или его отсутствии - ошибка" );
							Console.WriteLine( "  -remove - удаляет хэш MD5 и его поток из файла" );
							Console.WriteLine( "  -ok+    - по умолчанию, для файлов без ошибок выдает сообщение" );
							Console.WriteLine( "  -ok-    - для файлов без ошибок не выдает никаких сообщений" );
							Console.WriteLine( "  -ren    - для файлов с ошибками добавляет в название " + WrongExtension );
							Console.WriteLine( "  -s      - рекурентное выполнение для всех вложенных поддиректорий" );
							Console.WriteLine( "  -pause  - пауза на ввод любого символа после выполнения всей работы" );
							Console.WriteLine( "  -extract <dir> - в директории dir создает файл, одноименный проверяемому" );
							Console.WriteLine( "            с добавкой .MD5, куда записывает хэш файла;" );
							Console.WriteLine( "            при обработке директорий в dir создаются вложенные директории" );
							Console.WriteLine( "  -do <folder or mask> - если название обрабатываемой директории начинается с" );
							Console.WriteLine( "            минуса, перед ней надо поставить -do" );
//							Console.WriteLine( "  -cont[inue] <datetime> пропустить файлы, в которых отметка проверки позже" );
							Console.WriteLine( "Ключи -md5, -check и -remove взаимоисключающие," );
							Console.WriteLine( "если ничего не указано, работает -check" );
							return true;

						case "-pause":
							WorkPause = true;
							break;

						case "-remove":
							if( WorkCheck )
							{
								Console.WriteLine( "Параметр -remove не может использоваться вместе с параметром -check" );
								fault = true;
							}
							if( WorkMD5 )
							{
								Console.WriteLine( "Параметр -remove не может использоваться вместе с параметром -md5" );
								fault = true;
							}
							WorkRemove = true;
							break;

						case "-md5":
							if( WorkRemove )
							{
								Console.WriteLine( "Параметр -md5 не может использоваться вместе с параметром -remove" );
								fault = true;
							}
							if( WorkCheck )
							{
								Console.WriteLine( "Параметр -md5 не может использоваться вместе с параметром -check" );
								fault = true;
							}
							WorkMD5 = true;
							break;

						case "-check":
							if( WorkRemove )
							{
								Console.WriteLine( "Параметр -check не может использоваться вместе с параметром -remove" );
								fault = true;
							}
							if( WorkMD5 )
							{
								Console.WriteLine( "Параметр -check не может использоваться вместе с параметром -md5" );
								fault = true;
							}
							WorkCheck = true;
							break;

						case "-ok+":
							if( WorkOkMinus )
							{
								Console.WriteLine( "Параметр -ok+ не может использоваться вместе с параметром -ok-" );
								fault = true;
							}
							WorkOkPlus = true;
							break;

						case "-ok-":
							if( WorkOkPlus )
							{
								Console.WriteLine( "Параметр -ok- не может использоваться вместе с параметром -ok+" );
								fault = true;
							}
							WorkOkMinus = true;
							break;

						case "-extract":
							WorkExtract = true;
							catchExtract = true;
							break;

						case "-ren":
							WorkRen = true;
							break;

						case "-s":
							WorkSubFolders = true;
							break;

						default:
							Console.WriteLine( "Неизвестный параметр \"" + s + "\"" );
							fault = true;
							break;
						}
					}
					else
					{
						catchDo = true;
					}
				}
				if( catchDo )
				{
					if( s.IndexOfAny( Path.GetInvalidPathChars() ) >= 0 )
					{
						Console.WriteLine( "Недопустимый символ в параметре \"" + s + "\"" );
						fault = true;
					}
					if( Path.GetDirectoryName( s ).IndexOfAny( new char[] { '?', '*' } ) >= 0 )
					{
						Console.WriteLine( "Путь не может содержать символы маски (? или *), маска допустима только в конце" );
						fault = true;
					}

					Work.Add( s );
					catchDo = false;
				}
			}
			if( WorkExtract && catchExtract )
			{
				Console.WriteLine( "За ключем -extract должна быть указана директория" );
				fault = true;
			}
			return fault;
		}

		private static void Process( string entryName )
		{
			Console.WriteLine( "Processing: " + entryName );

			string fileName = Path.GetFileName( entryName );
			string dirName = Path.GetDirectoryName( entryName );
			if( String.IsNullOrEmpty( dirName ) )
				dirName = Environment.CurrentDirectory;

			if( String.IsNullOrEmpty( fileName ) )
			{
				CurTopDir = Path.GetDirectoryName( Path.GetFullPath( dirName ) );
				ProcessDirectory( dirName, "*" );
			}
			else
			{
				string[] dirs = null;
				try
				{
					dirs = Directory.GetDirectories( dirName, fileName, SearchOption.TopDirectoryOnly );
				}
				catch( DirectoryNotFoundException )
				{
					Console.WriteLine( "Указана отсутствующая директория для обработки: " + dirName );
					return;
				}
				if( dirs != null && dirs.Length > 0 )
				{
					CurTopDir = Path.GetDirectoryName( Path.GetFullPath( entryName ) );
					ProcessDirectory( entryName, "*" );
				}
				else
				{
					CurTopDir = Path.GetDirectoryName( Path.GetFullPath( dirName ) );
					ProcessDirectory( dirName, fileName );
				}
			}
		}

		private static void ProcessDirectory( string directoryName, string mask )
		{
			string[] entryName = Directory.GetFiles( directoryName, mask, SearchOption.TopDirectoryOnly );
			if( entryName != null )
			{
				foreach( string name in entryName )
				{
					bool setReadOnly = false;
					FileInfo f = new FileInfo( name );
					try
					{
						if( f.IsReadOnly )
						{
							// Если у файла атрибут read-only работа со стримами файла дает ошибку.
							// Сначала снимаем атрибут, затем ставим на место
							setReadOnly = true;
							f.IsReadOnly = false;
						}

						ProcessFile( name );
					}
					finally
					{
						if( setReadOnly )
							f.IsReadOnly = true;
					}
				}
			}

			if( WorkSubFolders )
			{
				entryName = Directory.GetDirectories( directoryName, mask, SearchOption.TopDirectoryOnly );
				if( entryName != null )
				{
					foreach( string name in entryName )
					{
						ProcessDirectory( name, "*" );
					}
				}
			}
		}

		private static void ProcessFile( string fileName )
		{
			if( Path.GetExtension( fileName ).ToLower() == ExtractExtension.ToLower() &&
				Path.GetFullPath( fileName ).StartsWith( ExtractFullPath ) )
				return; // Файлы с расширением .md5 из каталога -extract исключаются

			byte[] hash = null;

			using( FileStream fileStream = File.OpenRead( fileName ) )
			{
				hash = System.Security.Cryptography.MD5.Create().ComputeHash( fileStream );
			}

			FileInfo file = new FileInfo( fileName );

			if( WorkRemove && file.AlternateDataStreamExists( FileCheckedMarkerStreamName ) )
			{
				AlternateDataStreamInfo s = file.GetAlternateDataStream( FileCheckedMarkerStreamName, FileMode.Open );
				s.Delete();

				if( WorkOkPlus )
				{
					Console.WriteLine( "MD5 is removed:   " + GetShortName( fileName ) );
				}
			}

			if( WorkMD5 )
			{
				if( WorkExtract )
				{
					string extractTo = Path.Combine( ExtractDir, GetShortName( fileName ) + ExtractExtension );
					string extractToDir = Path.GetDirectoryName( extractTo );
					try
					{
						if( !Directory.Exists( extractToDir ) )
							Directory.CreateDirectory( extractToDir );
					}
					catch( Exception ex )
					{
						Console.WriteLine( "Ошибка создания директория для извлечения хэшей MD5: " + extractToDir );
						Console.WriteLine( ex.Message );
						throw;
					}
					StringBuilder sb = new StringBuilder();
					for( int i = 0; i < hash.Length; i++ )
					{
						sb.Append( hash[ i ].ToString( "X2" ) );
					}
					File.WriteAllText( extractTo, sb.ToString() );
				}
				else
				{
					AlternateDataStreamInfo s = file.GetAlternateDataStream( FileCheckedMarkerStreamName, FileMode.Create );
					using( FileStream writer = s.OpenWrite() )
					{
						writer.Write( hash, 0, hash.Length );
					}
					AlternateDataStreamInfo st = file.GetAlternateDataStream( FileDateTimeLastCheckStreamName, FileMode.Create );
					using( FileStream writer = st.OpenWrite() )
					using( TextWriter tr = new StreamWriter( writer ) )
					{
						tr.Write( DateTime.Now.ToString( "yyyy-MM-dd HH:mm:ss" ) );
					}
				}

				if( WorkOkPlus )
				{
					Console.WriteLine( "MD5 is done:      " + GetShortName( fileName ) );
				}
			}

			if( WorkCheck )
			{
				bool wellDone = false;
				byte[] bytes = null;

				if( WorkExtract )
				{
					string extractTo = Path.Combine( ExtractDir, GetShortName( fileName ) + ExtractExtension );
					try
					{
						bytes = ConvertHexStringToByteArray( File.ReadAllText( extractTo ) );
					}
					catch( Exception ex )
					{
						Console.WriteLine( "Ошибка чтения хэша MD5 из файла. Проверьте правильность параметра -extract и содержимое файла." );
						Console.WriteLine( "Попытка чтения MD5 из файла: " + extractTo );
						Console.WriteLine( ex.Message );
						throw;
					}
					if( bytes != null )
						wellDone = true;
				}
				else
				{
					if( file.AlternateDataStreamExists( FileCheckedMarkerStreamName ) )
					{
						AlternateDataStreamInfo s = file.GetAlternateDataStream( FileCheckedMarkerStreamName, FileMode.Open );
						if( s.Size < 1000 )
						{
							using( FileStream reader = s.OpenRead() )
							{
								bytes = new byte[ s.Size ];
								int n = reader.Read( bytes, 0, bytes.Length );
								if( n == bytes.Length )
									wellDone = true;
							}
						}
					}
				}
				bool eq = true;
				if( !wellDone )
				{
					Console.WriteLine( "Missing MD5:      " + GetShortName( fileName ) );
					eq = false;
				}
				else
				{
					if( bytes != null && hash != null )
					{
						if( bytes.Length != hash.Length )
						{
							Console.WriteLine( "Разная длина MD5: " + GetShortName( fileName ) );
							eq = false;
						}
						else
						{
							for( int i = 0; eq && i < bytes.Length; i++ )
								eq = bytes[ i ] == hash[ i ];
							if( !eq )
							{
								Console.WriteLine( "Wrong MD5:        " + GetShortName( fileName ) );
							}
						}
					}
				}
				if( !eq && WorkRen )
				{
					try
					{
						File.Move( fileName, fileName + WrongExtension );
					}
					catch( Exception ex )
					{
						Console.WriteLine( "Ошибка переименования файла " + GetShortName( fileName ) );
						Console.WriteLine( ex.Message );
						throw;
					}
				}
				if( eq && WorkOkPlus )
				{
					Console.WriteLine( "MD5 is OK:        " + GetShortName( fileName ) );
				}
			}
		}
		private static string GetShortName( string name )
		{
			return MakeRelativePath( CurTopDir, name );
			//string fullName = Path.GetFullPath( name );
			//if( fullName.StartsWith( CurTopDir, StringComparison.CurrentCultureIgnoreCase ) )
			//{
			//	string s = fullName.Remove( 0, CurTopDir.Length );
			//	while( s.StartsWith( @"\" ) )
			//	{
			//		if( s.Length == 1 ) return "";
			//		s = s.Remove( 0, 1 );
			//	}
			//	return s;
			//}
			//else
			//	return fullName;
		}
		/// <summary>
		/// Creates a relative path from one file or folder to another.
		/// Источник: https://stackoverflow.com/questions/275689/how-to-get-relative-path-from-absolute-path
		/// </summary>
		/// <param name="fromPath">Contains the directory that defines the start of the relative path.</param>
		/// <param name="toPath">Contains the path that defines the endpoint of the relative path.</param>
		/// <returns>The relative path from the start directory to the end path or <c>toPath</c> if the paths are not related.</returns>
		/// <exception cref="ArgumentNullException"></exception>
		/// <exception cref="UriFormatException"></exception>
		/// <exception cref="InvalidOperationException"></exception>
		public static String MakeRelativePath( String fromPath, String toPath )
		{
			if( String.IsNullOrEmpty( fromPath ) ) throw new ArgumentNullException( "fromPath" );
			if( String.IsNullOrEmpty( toPath ) ) throw new ArgumentNullException( "toPath" );

			Uri fromUri = new Uri( fromPath );
			Uri toUri = new Uri( toPath );

			if( fromUri.Scheme != toUri.Scheme ) { return toPath; } // path can't be made relative.

			Uri relativeUri = fromUri.MakeRelativeUri( toUri );
			String relativePath = Uri.UnescapeDataString( relativeUri.ToString() );

			if( toUri.Scheme.Equals( "file", StringComparison.InvariantCultureIgnoreCase ) )
			{
				relativePath = relativePath.Replace( Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar );
			}

			return relativePath;
		}
		public static byte[] ConvertHexStringToByteArray( string hexString )
		{
			if( hexString.Length % 2 != 0 )
				return null;

			byte[] HexAsBytes = new byte[ hexString.Length / 2 ];
			for( int index = 0; index < HexAsBytes.Length; index++ )
			{
				string byteValue = hexString.Substring( index * 2, 2 );
				HexAsBytes[ index ] = byte.Parse( byteValue, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture );
			}

			return HexAsBytes;
		}

	}
}