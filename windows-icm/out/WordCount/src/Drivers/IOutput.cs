namespace App.Drivers
{
    // The seam Core's callers are written against. Implementations are flat siblings here.
    public interface IOutput
    {
        void Write(string line);
    }
}