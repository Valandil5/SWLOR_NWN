using SWLOR.Game.Server.Legacy.Data.Entity;

namespace SWLOR.Game.Server.Legacy.Caching
{
    public class BankCache: CacheBase<Bank>
    {
        protected override void OnCacheObjectSet(Bank entity)
        {
        }

        protected override void OnCacheObjectRemoved(Bank entity)
        {
        }

        protected override void OnSubscribeEvents()
        {
        }

        public Bank GetByID(int id)
        {
            return (Bank)ByID[id].Clone();
        }

    }
}