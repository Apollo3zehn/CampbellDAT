# CampbellDAT

[![AppVeyor](https://ci.appveyor.com/api/projects/status/github/apollo3zehn/campbelldat?svg=true)](https://ci.appveyor.com/project/Apollo3zehn/campbelldat) [![NuGet](https://img.shields.io/nuget/vpre/CampbellDAT.svg?label=Nuget)](https://www.nuget.org/packages/CampbellDAT)

CampbellDAT is a simple and feature-limited library that provides a reader for Campbell files stored in the TOB1, TOB2 or TOB3 format (.dat).

The following code shows how to read a specific variable:

```cs
var filePath = "testdata.dat";

using (var campbellFile = new CampbellFile(filePath))
{
    Console.WriteLine($"File '{filePath}' contains {campbellFile.Variables.Count} variables.");

    var variable = campbellFile.Variables[0];
    (var timestamps, var data) = campbellFile.Read<float>(variable);

    Console.WriteLine($"Variable '{data.Variable.Name}' is of type {data.Variable.DataType}.");
    Console.WriteLine($"    The first value is {data.Buffer[0]}.");
}
```

If you want to read a variable of type `string`, please use the method `campbellFile.ReadString(variable);` instead.


## Advanced features

As long as the number of bytes of the generic type (here: ```float```) matches the number of bytes of the variable type (```variable.DataType```), you can provide any numeric type. For example, instead of interpreting the data as ```float```, you can also do the following:

```cs
(var timestamps, var data) = campbellFile.Read<uint>(variable);
```

This works since both, ```float``` and ```uint32``` have a length of 4 bytes. Of course, interpreting actual ```float``` data as ```uint32``` will result in meaningless numbers, but this feature may be useful to reinterpret other types like ```int32``` vs. ```uint32```.

## See also

This implementation is based on [LoggerNet Instruction Manual v4.7, Appendix B](https://s.campbellsci.com/documents/us/manuals/loggernet.pdf) and https://github.com/ansell/camp2ascii.
