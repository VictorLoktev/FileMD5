using System;
using System.Linq;
using System.IO;
using System.Reflection;

namespace FileMD5
{
	public class Program
	{

		static int Main( string[] args )
		{
			/*
			 * Чтобы программы была из одного файла (.exe)
			 * без дополнительных сборок (.dll),
			 * все сборки интегрируются ресурсами в исполняемый модуль.
			 * При обращении к сборкам обработчик по событию выдает код сборки
			 * не из файловой системы, а из ресурса.
			 */
			AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;

			Processor processor = new Processor();
			return processor.Run( args );

		}

		private static Assembly CurrentDomain_AssemblyResolve( object sender, ResolveEventArgs args )
		{
			// Source: https://stackoverflow.com/questions/10137937/merge-dll-into-exe

			Assembly thisAssembly = Assembly.GetExecutingAssembly();

			return AssemblyResolve( args.Name );
		}

		private static Assembly AssemblyResolve( string name )
		{
			// Source: https://stackoverflow.com/questions/10137937/merge-dll-into-exe

			Assembly thisAssembly = Assembly.GetExecutingAssembly();

			// Get the Name of the AssemblyFile
			if( !name.EndsWith( ".dll" ) )
			{
				int index = name.IndexOf( ',' );
				if( index > 0 )
					name = name.Substring( 0, index );
				name += ".dll";
			}

			// Take the Assembly form Embedded Resources
			var resources = thisAssembly.GetManifestResourceNames().Where( s => s.EndsWith( name ) );
			if( resources.Count() > 0 )
			{
				var resourceName = resources.First();
				using( Stream stream = thisAssembly.GetManifestResourceStream( resourceName ) )
				{
					if( stream == null ) return null;
					var block = new byte[ stream.Length ];
					stream.Read( block, 0, block.Length );
					Assembly asm = Assembly.Load( block );
					AssemblyName[] asmNames = asm.GetReferencedAssemblies();
					return asm;
				}
			}
			return null;
		}
	}
}
