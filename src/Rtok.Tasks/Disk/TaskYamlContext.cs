using SharpYaml.Serialization;

namespace Rtok.Tasks.Disk;

[YamlSerializable(typeof(TaskHead))]
internal sealed partial class TaskYamlContext : YamlSerializerContext;
