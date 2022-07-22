# CampbellDAT

[![GitHub Actions](https://github.com/Apollo3zehn/CampbellDAT/actions/workflows/build-and-publish.yml/badge.svg)](https://github.com/Apollo3zehn/CampbellDAT/actions) [![NuGet](https://img.shields.io/nuget/vpre/CampbellDAT.svg?label=Nuget)](https://www.nuget.org/packages/CampbellDAT)

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

If you prefer working with raw buffers (`byte[]`) instead, just pass `byte` as generic parameter into the read method:

```cs
(var timestamps, var data) = campbellFile.Read<byte>(variable);
```

If you want to read a variable of type `string`, please use the method `campbellFile.ReadString(variable);` instead.

## See also

This implementation is based on [LoggerNet Instruction Manual v4.7, Appendix B](https://s.campbellsci.com/documents/us/manuals/loggernet.pdf) and https://github.com/ansell/camp2ascii.
