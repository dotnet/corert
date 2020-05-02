# Troubleshooting CoreRT compiler

Sometimes you want to have more information how CoreRT work. ILC Compiler provide couple switches for that:

* `<IlcGenerateMetadataLog>true</IlcGenerateMetadataLog>`: Enable generation of metadata log. This class is CSV format with following structure: `Handle, Kind, Name, Children`.
* `<IlcGenerateDgmlFile>true</IlcGenerateDgmlFile>`: Generates log files `ProjectName.codegen.dgml.xml` and `ProjectName.scan.dgml.xml` in DGML format.
* `<IlcGenerateMapFile>true</IlcGenerateMapFile>`: Generates log files `ProjectName.map.xml` which describe layout of objects how CoreRT sees them.
* `<IlcSingleThreaded>true</IlcSingleThreaded>`: Perform compilation on single thread.
* `<IlcDumpIL>true</IlcDumpIL>`: Dump final IL after generatoin in the file `ProjectName.il`. This can be helpful when debugging IL transformation, like marshalling for example.
