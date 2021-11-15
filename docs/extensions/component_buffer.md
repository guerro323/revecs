## Component Buffer

A component buffer contains an internal resizable list.

## Usage

```cs
var intBuffer = world.RegisterComponent<BufferComponentSetup<int>, int>();

var entity = world.CreateEntity();
world.AddComponent(entity, intBuffer, new[] { 1, 2, 3 });

foreach (int v in world.ReadComponent(entity, intBuffer))
{
    // will output 1 2 3
}

// List operation:
var mutableBuffer = world.AsBufferType(intBuffer);

var buffer = world.ReadBuffer(entity, mutableBuffer);
buffer.Clear();
buffer.Add(42);

foreach (int v in world.ReadComponent(entity, intBuffer))
{
    // will output 42
}
```

## Source Generator

```cs
partial struct MyBuffer : IBufferComponent
{
    public int Value;
}

var myBuffer = MyBuffer.Type.GetOrCreate(world);
```