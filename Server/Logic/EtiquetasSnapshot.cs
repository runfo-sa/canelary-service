using Core;
using System.Collections.Frozen;

namespace Server.Logic
{
    /// <summary>
    /// Singleton holder del snapshot actual de Etiquetas. El read es lock-free; el write
    /// es atomico via <see cref="Interlocked.Exchange{T}(ref T, T)"/>. Lo populan/refrescan
    /// los pollers (ver <see cref="EtiquetasPollingService"/>).
    /// </summary>
    public sealed class EtiquetasSnapshot
    {
        private FrozenSet<Etiqueta> _current = FrozenSet<Etiqueta>.Empty;

        public FrozenSet<Etiqueta> Current => Volatile.Read(ref _current);

        public void Replace(FrozenSet<Etiqueta> next)
        {
            Interlocked.Exchange(ref _current, next);
        }
    }
}
