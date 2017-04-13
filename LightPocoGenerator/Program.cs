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
                Console.WriteLine("Arguments must be <OutputPath>[, <Namespace>].");
                Console.WriteLine("Enter the some key.");
                Console.ReadLine();
                return;
            }

            var outputPath = args[0];

            Console.WriteLine($"Output Path : {outputPath}");
            Console.WriteLine("");

            var connectionSettings = ConfigurationManager.ConnectionStrings
                .Cast<ConnectionStringSettings>()
                .ToArray();

            foreach (var connectionSetting in connectionSettings)
            {
                var factory = DbProviderFactories.GetFactory(connectionSetting.ProviderName);

                using (var connection = factory.CreateConnection())
                {
                    if (connection == null)
                        throw new NullReferenceException();

                    var outputDirectory = Path.Combine(outputPath, connectionSetting.Name);

                    var directory = new DirectoryInfo(outputDirectory);

                    if (!directory.Exists)
                    {
                        directory.Create();

                        Console.WriteLine($"{outputDirectory} is created.");
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

                    var @namespace = args.Skip(1).Any() ? $"{args[1]}.{connectionSetting.Name}" : connectionSetting.Name;

                    connection.ConnectionString = connectionSetting.ConnectionString;
                    connection.Open();

                    var tables = connection.GetSchema("Columns")
                        .AsEnumerable()
                        .Select(x => new
                        {
                            TableName       = (string)x["TABLE_NAME"],
                            ColumnName      = (string)x["COLUMN_NAME"],
                            DataType        = (string)x["DATA_TYPE"],
                            IsNullable      = ((string)x["IS_NULLABLE"] == "YES"),
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
                            TypeName = (string)x["TypeName"],
                            DataType = Type.GetType((string)x["DataType"]),
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

                        var file = Path.Combine(outputDirectory, $"{table.Key}.cs");

                        File.WriteAllText(file, outputText.ToString(), Encoding.UTF8);

                        Console.WriteLine($"{table.Key}.cs is created.");
                    }
                    
                    Console.WriteLine("");
                }
            }
            
            Console.WriteLine("Enter the some key.");
            Console.ReadLine();
        }
    }
}
