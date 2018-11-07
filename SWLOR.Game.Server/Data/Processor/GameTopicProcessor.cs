﻿using FluentValidation;
using SWLOR.Game.Server.Data.Contracts;
using SWLOR.Game.Server.Data.Entity;
using SWLOR.Game.Server.Data.Validator;
using SWLOR.Game.Server.Service.Contracts;
using SWLOR.Game.Server.ValueObject;

namespace SWLOR.Game.Server.Data.Processor
{
    public class GameTopicProcessor : IDataProcessor<GameTopic>
    {
        public IValidator Validator => new GameTopicValidator();

        public DatabaseAction Process(IDataService data, GameTopic dataObject)
        {
            return null;
        }
    }
}