<!--icm
{
  "id": "proxy",
  "title": "Proxy pattern (a stand-in that controls access to a real object)",
  "doc_type": "pattern",
  "summary": "Puts a placeholder in front of a real object that shares its interface and controls access to it, here deferring the expensive real object's creation until first use (virtual proxy).",
  "keywords": ["proxy", "virtual proxy", "lazy loading", "placeholder", "Image", "ProxyImage", "RealImage", "access control", "structural pattern"],
  "source": { "origin": "authored", "repo_path": "DesignPatterns/ProxyPattern", "note": "user's own compiling C# example (Head First Design Patterns style); source targets .NET 6, distilled to the C# 5 form this ICM uses" }
}
-->

# Proxy pattern

## Intent
Provide a surrogate or placeholder for another object to control access to it. The proxy implements the same interface as the real subject and decides when and how to forward calls. This example is a virtual proxy: it delays constructing the expensive `RealImage` (which "loads from disk") until `display()` is first called, then caches it.

## When to use
- Creating or holding the real object is expensive and you want to defer it (virtual proxy / lazy loading).
- You need to add access control, remoting, logging, or caching around an object without changing it (protection / remote / caching proxies).
- Not needed when the real object is cheap and always required up front.

## Participants
- Subject (the shared interface) - `Image` (`display()`)
- RealSubject (the expensive real object) - `RealImage` (loads from disk in its constructor)
- Proxy (same interface; holds the file name, lazily creates and delegates to the RealSubject) - `ProxyImage`
- Client - `Program`

## Key code
```csharp
using System;

namespace ProxyPattern
{
    // Subject: the interface both the real object and the proxy share.
    public interface Image
    {
        void display();
    }

    // RealSubject: construction is the expensive part (simulated disk load).
    public class RealImage : Image
    {
        private string _fileName;

        public RealImage(string fileName)
        {
            _fileName = fileName;
            loadFromDisk(_fileName);
        }

        public void display()
        {
            Console.WriteLine("Displaying " + _fileName);
        }

        private void loadFromDisk(string fileName)
        {
            Console.WriteLine("Loading " + fileName);
        }
    }

    // Proxy: cheap to create; builds the RealImage only on the first display() call,
    // then reuses it on later calls.
    public class ProxyImage : Image
    {
        private RealImage _realImage;
        private string _fileName;

        public ProxyImage(string fileName)
        {
            _fileName = fileName;
        }

        public void display()
        {
            if (_realImage == null)
            {
                _realImage = new RealImage(_fileName);
            }
            _realImage.display();
        }
    }
}
```

## How it wires (from Program.cs)
```csharp
using System;

namespace ProxyPattern
{
    class Program
    {
        static void Main(string[] args)
        {
            Image image = new ProxyImage("testImage.jpg");

            //image will be loaded from disk
            image.display();   // first call: builds RealImage (prints "Loading ...") then displays
            Console.WriteLine("");

            //image will not be loaded from disk
            image.display();   // second call: reuses cached RealImage, no load
        }
    }
}
```

## C# 5 notes
- This example is fully C# 5 compatible. No string interpolation, expression-bodied members, null-conditional operators, or other C#-6+ syntax is used.
- The only adjustment versus the source is the implicit-usings reliance: add `using System;` explicitly in files that use `Console` (`RealImage`, `Program`). `ProxyImage` and the `Image` interface need no usings.
- Note the interface is named `Image` (not `IImage`); keep the name as authored. Method `display()` is lower-cased as authored.
