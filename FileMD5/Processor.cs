using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using Trinet.Core.IO.Ntfs;

namespace FileMD5
{
	public partial class Processor
	{
		const string FileCheckedMarkerStreamName = "MD5";
		const string FileDateTimeLastCheckStreamName = "MD5-Last-Check-DateTime";
		const string ProcessingPrefix = "Processing: ";
		const string ExtractExtension = ".MD5";
		const string WrongExtension = ".!!!WrongMD5!!!";
		private static readonly List<string> Work = new List<string>();
		private static string ExtractDir = null;
		private static string ExtractFullPath = null;

		private static string CurTopDir;

		private static bool CheckMD5Option = false;
		private static bool SetMD5Option = false;
		private static bool OkPlusOption = false;
		private static bool OkMinusOption = false;
		private static bool RenameOption = false;
		private static bool OfflineAttributeOption = false;
		private static bool SubFoldersOption = false;
		private static bool PauseOption = false;
		private static bool BeepOption = false;
		private static bool RemoveOption = false;
		private static bool ExtractOption = false;

		private volatile static int TotalProcessedFilesCounter = 0;
		private volatile static int MissingMd5FilesCounter = 0;
		private volatile static int WrongMd5FilesCounter = 0;
		private volatile static int SetMd5FilesCounter = 0;
		private volatile static int RemovedMd5FilesCounter = 0;
		private volatile static int ErrorFilesCounter = 0;

		private static StringBuilder ProcessText;
		private static StringBuilder CleanText;

		public static object StringBuider { get; private set; }

		public int Run( string[] args )
		{
			string text = "FileMD5 - Контроль целостности файлов.  Используйте ключ -? для справки.";
			Console.WriteLine( text );
			// Если либо стандартный вывод, либо вывод ошибок перенаправлен в файл, то дублируем вывод сообщения в оба канала
			if( Console.IsErrorRedirected != Console.IsOutputRedirected )
				Console.Error.WriteLine( text );

			bool fault = ProcessArguments( args );
			if( !SetMD5Option && !RemoveOption ) CheckMD5Option = true;
			if( !OkMinusOption ) OkPlusOption = true;

			if( !fault )
			{
				ProcessText = new StringBuilder( Console.WindowWidth );
				CleanText = new StringBuilder( "\r".PadLeft( Console.WindowWidth - 1 ) );

				if( Work.Count == 0 )
				{
					Work.Add( Environment.CurrentDirectory );
					text = "Файл или директория в параметре не заданы, обрабатывается текущая директория";
					Console.WriteLine( text );
					if( Console.IsErrorRedirected != Console.IsOutputRedirected )
						Console.Error.WriteLine( text );
				}
				Stopwatch timer = Stopwatch.StartNew();
				int workCounter = 0;
				foreach( string processingItem in Work )
				{
					workCounter++;
					text = string.Format(
						"\r\nЭлемент обработки {0} №{1}: {2}\r\n",
						SubFoldersOption ? "(с поддиректориями)" : "(без поддиректорий)",
						workCounter,
						processingItem );
					Console.WriteLine( text );
					if( Console.IsErrorRedirected != Console.IsOutputRedirected )
						Console.Error.WriteLine( text );


					ProcessWorkItem( processingItem );
				}

				text =
					$"\r\nОбработка завершена за {timer.Elapsed}\r\n" +
					$"Всего обработано файлов: {TotalProcessedFilesCounter}  из них:\r\n" +
					$"Ошибок обработки файла:  {ErrorFilesCounter}\r\n" +
					$"Установлен хеш:          {SetMd5FilesCounter}\r\n" +
					$"Удален хеш:              {RemovedMd5FilesCounter}\r\n" +
					$"Отсутствует хеш:         {MissingMd5FilesCounter}\r\n" +
					$"Неправильный хеш:        {WrongMd5FilesCounter}\r\n"
					;
				Console.WriteLine( text );
				if( Console.IsErrorRedirected != Console.IsOutputRedirected )
					Console.Error.WriteLine( text );
			}
			if( BeepOption )
			{
				Console.Beep();
			}
			if( PauseOption )
			{
				text = "Нажмите любую кнопку для завершения";
				// Пытаемся вывести сообщение на консоль, а не в перенаправленный поток
				if( !Console.IsOutputRedirected )
					Console.WriteLine( text );
				else
				if( !Console.IsErrorRedirected )
					Console.Error.WriteLine( text );
				else
					Console.WriteLine( text );

				Console.ReadKey( true );
			}

			return fault ? 1 : 0;
		}

		private static void ProcessWorkItem( string entryName )
		{
			string fileName = Path.GetFileName( entryName );
			string dirName = Path.GetDirectoryName( entryName );
			if( string.IsNullOrEmpty( dirName ) )
				dirName = Environment.CurrentDirectory;

			if( string.IsNullOrEmpty( fileName ) )
			{
				CurTopDir = Path.GetDirectoryName( Path.GetFullPath( dirName ) );
				ProcessDirectory( dirName, "*" );
			}
			else
			{
				string[] dirs;
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
							// Если у файла атрибут read-only работа с потоками файла дает ошибку.
							// Сначала снимаем атрибут, затем ставим на место.
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

			if( SubFoldersOption )
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

			ProgressLine( fileName );

			byte[] hash = null;
			FileInfo file = new FileInfo( fileName );

			if( RemoveOption && file.AlternateDataStreamExists( FileCheckedMarkerStreamName ) )
			{
				AlternateDataStreamInfo s = file.GetAlternateDataStream( FileCheckedMarkerStreamName, FileMode.Open );
				s.Delete();
				Interlocked.Increment( ref RemovedMd5FilesCounter );

				if( OkPlusOption )
				{
					ClearLine();
					Console.WriteLine( "MD5 is removed:   " + GetShortName( fileName ) );
				}
			}

			if( SetMD5Option )
			{
				if( ExtractOption )
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
						ClearLine();
						Console.WriteLine( string.Concat(
							"Ошибка создания директория для извлечения хешей MD5: ",
							extractToDir,
							"\r\n",
							ex.Message ) );
						throw;
					}
					StringBuilder sb = new StringBuilder();

					if( hash == null )
					{
						using( FileStream fileStream = File.Open( fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite ) )
						{
							hash = System.Security.Cryptography.MD5.Create().ComputeHash( fileStream );
						}
					}

					for( int i = 0; i < hash.Length; i++ )
					{
						sb.Append( hash[ i ].ToString( "X2" ) );
					}
					File.WriteAllText( extractTo, sb.ToString() );
				}
				else
				{
					if( hash == null )
					{
						using( FileStream fileStream = File.Open( fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite ) )
						{
							hash = System.Security.Cryptography.MD5.Create().ComputeHash( fileStream );
						}
					}

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

				/*
				 * Если при проверке файлу был установлен атрибут Offline
				 * из-за ошибки или отсутствия хеша,
				 * то этот атрибут надо сбросить.
				 */
				var attributes = File.GetAttributes( fileName );
				if( attributes.HasFlag( FileAttributes.Offline ) )
				{
					attributes &= ~FileAttributes.Offline;
					File.SetAttributes( fileName, attributes );
				}

				if( OkPlusOption )
				{
					ClearLine();
					Console.WriteLine( "MD5 is set:       " + GetShortName( fileName ) );
				}
				Interlocked.Increment( ref SetMd5FilesCounter );
			}

			if( CheckMD5Option )
			{
				byte[] bytes = null;

				if( ExtractOption )
				{
					string extractTo = Path.Combine( ExtractDir, GetShortName( fileName ) + ExtractExtension );
					try
					{
						bytes = ConvertHexStringToByteArray( File.ReadAllText( extractTo ) );
					}
					catch( Exception ex )
					{
						ClearLine();
						Console.WriteLine(
							"Ошибка чтения хеша MD5 из файла.\r\n" +
							"Проверьте правильность параметра -extract и содержимое файла.\r\n" +
							"Попытка чтения MD5 из файла:\r\n" +
							$"  {extractTo}\r\n" +
							$"{ex.Message}" );
						throw;
					}
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
								if( n != bytes.Length )
									bytes = null;
							}
						}
					}
				}
				bool eq = true;
				if( bytes == null )
				{
					ClearLine();
					Console.WriteLine( "Missing MD5:      " + GetShortName( fileName ) );
					eq = false;
					Interlocked.Increment( ref MissingMd5FilesCounter );
				}
				else
				{
					if( hash == null )
					{
						using( FileStream fileStream = File.Open( fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite ) )
						{
							hash = System.Security.Cryptography.MD5.Create().ComputeHash( fileStream );
						}
					}

					if( bytes.Length != hash.Length )
					{
						ClearLine();
						Console.WriteLine( "Разная длина MD5: " + GetShortName( fileName ) );
						eq = false;
						Interlocked.Increment( ref WrongMd5FilesCounter );
					}
					else
					{
						for( int i = 0; eq && i < bytes.Length; i++ )
							eq = bytes[ i ] == hash[ i ];
						if( !eq )
						{
							ClearLine();
							Console.WriteLine( "Wrong MD5:        " + GetShortName( fileName ) );
							Interlocked.Increment( ref WrongMd5FilesCounter );
						}
					}
				}
				if( !eq && OfflineAttributeOption )
				{
					try
					{
						var attributes = File.GetAttributes( fileName );
						attributes |= FileAttributes.Offline;
						File.SetAttributes( fileName, attributes );
					}
					catch( Exception ex )
					{
						ClearLine();
						Console.WriteLine( "Ошибка установки атрибута для файла " + GetShortName( fileName ) );
						Console.WriteLine( ex.Message );
						//throw;
						Interlocked.Increment( ref ErrorFilesCounter );
					}
				}
				if( !eq && RenameOption )
				{
					try
					{
						File.Move( fileName, fileName + WrongExtension );
					}
					catch( Exception ex )
					{
						ClearLine();
						Console.WriteLine( "Ошибка переименования файла " + GetShortName( fileName ) );
						Console.WriteLine( ex.Message );
						//throw;
						Interlocked.Increment( ref ErrorFilesCounter );
					}
				}
				if( eq && OkPlusOption )
				{
					ClearLine();
					Console.WriteLine( "MD5 is OK:        " + GetShortName( fileName ) );
				}
				// Увеличиваем счетчик общего количества обработанных файлов
				Interlocked.Increment( ref TotalProcessedFilesCounter );
			}
		}
		private static string GetShortName( string name )
		{
			return MakeRelativePath( CurTopDir, name );
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
		public static string MakeRelativePath( string fromPath, string toPath )
		{
			if( string.IsNullOrEmpty( fromPath ) ) throw new ArgumentNullException( "fromPath" );
			if( string.IsNullOrEmpty( toPath ) ) throw new ArgumentNullException( "toPath" );

			Uri fromUri = new Uri( fromPath );
			Uri toUri = new Uri( toPath );

			if( fromUri.Scheme != toUri.Scheme ) { return toPath; } // path can't be made relative.

			Uri relativeUri = fromUri.MakeRelativeUri( toUri );
			string relativePath = Uri.UnescapeDataString( relativeUri.ToString() );

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
				HexAsBytes[ index ] = byte.Parse( byteValue,
					System.Globalization.NumberStyles.HexNumber,
					System.Globalization.CultureInfo.InvariantCulture );
			}

			return HexAsBytes;
		}

		private static void ClearLine()
		{
			if( !Console.IsOutputRedirected )
			{
				Console.Write( CleanText );
			}
			else
			if( !Console.IsErrorRedirected )
			{
				Console.Error.Write( CleanText );
			}
		}

		private static void ProgressLine( string filePath )
		{
			ProcessText.Clear();
			ProcessText.Append( ProcessingPrefix );

			int maxLen = Console.WindowWidth - ProcessingPrefix.Length - 1;
			if( maxLen <= 0 )
				return;
			if( maxLen <= filePath.Length )
				filePath = filePath.Substring( 0, maxLen );

			ProcessText.Append( filePath.PadRight( maxLen ) );
			ProcessText.Append( '\r' );

			if( !Console.IsOutputRedirected )
			{
				Console.Write( ProcessText.ToString() );
			}
			else
			if( !Console.IsErrorRedirected )
			{
				Console.Error.Write( ProcessText.ToString() );
			}
		}

	}
}
