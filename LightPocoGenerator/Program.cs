using System;
using System.Configuration;
using System.Data;
using System.Data.Common;
using System.IO;
using System.Linq;
using System.Text;

namespace LightPocoGenerator
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args == null || !args.Any())
            {
                Console.WriteLine("Arguments must be <OutputPath>, <ConnectionStringName>[, <Namespace>].");
                Console.WriteLine("Enter the some key.");
                Console.ReadLine();
                return;
            }

            var outputPath           = args[0];
            var connectionStringName = args[1];
            var @namespace           = args.Skip(2).Any() ? args[2] : args[1];

            Console.WriteLine($"Output Path : {outputPath}");
            Console.WriteLine("");
            
            var directory = new DirectoryInfo(outputPath);

            if (!directory.Exists)
            {
                directory.Create();

                Console.WriteLine($"{outputPath} is created.");
                Console.WriteLine("");
            }

            var files = directory.GetFiles();
            if (files.Any())
            {
                foreach (var file in files)
                {
                    file.Delete();
                }
            }

            var connectionSetting = ConfigurationManager.ConnectionStrings[connectionStringName];

            var factory = DbProviderFactories.GetFactory(connectionSetting.ProviderName);

            using (var connection = factory.CreateConnection())
            {
                if (connection == null)
                    throw new NullReferenceException();

                connection.ConnectionString = connectionSetting.ConnectionString;
                connection.Open();

                var tables = connection.GetSchema("Columns")
                    .AsEnumerable()
                    .Select(x => new
                    {
                        TableName       = (string)x["TABLE_NAME"],
                        ColumnName      = (string)x["COLUMN_NAME"],
                        DataType        = (string)x["DATA_TYPE"],
                        IsNullable      = ((string)x["IS_NULLABLE"] == "NO"),
                        OrdinalPosition = (int)x["ORDINAL_POSITION"]
                    })
                    .GroupBy(x => x.TableName)
                    .OrderBy(x => x.Key)
                    .ToArray();

                var dataTypes = connection.GetSchema("DataTypes")
                    .AsEnumerable()
                    .Where(x => !(x["DataType"] is DBNull))
                    .Select(x => new
                    {
                        TypeName   = (string)x["TypeName"],
                        DataType   = Type.GetType((string)x["DataType"]),
                    })
                    .ToDictionary(x => x.TypeName);

                foreach (var table in tables)
                {
                    var outputText = new StringBuilder();

                    outputText.AppendLine("using System;")
                        .AppendLine("")
                        .AppendLine($"namespace {@namespace}")
                        .AppendLine("{")
                        .AppendLine("")
                        .AppendLine($"    public class {table.Key}")
                        .AppendLine("    {")
                        .AppendLine("");

                    foreach (var column in table.OrderBy(x => x.OrdinalPosition))
                    {
                        var dataType = dataTypes[column.DataType];
                        var typeName = dataType.DataType.Name;
                        if (column.IsNullable && dataType.DataType.IsValueType)
                            typeName = $"{typeName}?";

                        outputText.AppendLine($"        public {typeName} {column.ColumnName} {{ get; set; }}")
                            .AppendLine("");
                    }

                    outputText.AppendLine("    }")
                        .AppendLine("}");

                    var file = Path.Combine(outputPath, $"{table.Key}.cs");

                    File.WriteAllText(file, outputText.ToString(), Encoding.UTF8);

                    Console.WriteLine($"{table.Key}.cs is created.");
                }
            }
            
            Console.WriteLine("");
            Console.WriteLine("Enter the some key.");
            Console.ReadLine();
        }
    }
}
