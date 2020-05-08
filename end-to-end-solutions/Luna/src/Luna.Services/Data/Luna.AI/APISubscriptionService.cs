﻿using Luna.Clients.Azure.APIM;
using Luna.Clients.Exceptions;
using Luna.Clients.Logging;
using Luna.Data.Entities;
using Luna.Data.Repository;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Luna.Services.Data.Luna.AI
{
    public class APISubscriptionService : IAPISubscriptionService
    {
        private readonly ISqlDbContext _context;
        private readonly ILogger<APISubscriptionService> _logger;
        private readonly IAPISubscriptionAPIM _apiSubscriptionAPIM;

        /// <summary>
        /// Constructor that uses dependency injection.
        /// </summary>
        /// <param name="sqlDbContext">The context to be injected.</param>
        /// <param name="logger">The logger.</param>
        public APISubscriptionService(ISqlDbContext sqlDbContext, ILogger<APISubscriptionService> logger, IAPISubscriptionAPIM apiSubscriptionAPIM)
        {
            _context = sqlDbContext ?? throw new ArgumentNullException(nameof(sqlDbContext));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _apiSubscriptionAPIM = apiSubscriptionAPIM ?? throw new ArgumentNullException(nameof(apiSubscriptionAPIM)); ;
        }

        public async Task<List<APISubscription>> GetAllAsync(string[] status = null, string owner = "")
        {
            _logger.LogInformation(LoggingUtils.ComposeGetAllResourcesMessage(typeof(APISubscription).Name));

            // Get all apiSubscriptions
            var apiSubscriptions = await _context.APISubscriptions.ToListAsync();
            _logger.LogInformation(LoggingUtils.ComposeReturnCountMessage(typeof(APISubscription).Name, apiSubscriptions.Count()));

            return apiSubscriptions;
        }

        public async Task<APISubscription> GetAsync(Guid apiSubscriptionId)
        {
            if (!await ExistsAsync(apiSubscriptionId))
            {
                throw new LunaNotFoundUserException(LoggingUtils.ComposeNotFoundErrorMessage(typeof(APISubscription).Name,
                    apiSubscriptionId.ToString()));
            }
            _logger.LogInformation(LoggingUtils.ComposeGetSingleResourceMessage(typeof(APISubscription).Name, apiSubscriptionId.ToString()));

            // Get the product that matches the provided productName
            var apiSubscription = await _context.APISubscriptions.SingleOrDefaultAsync(o => (o.SubscriptionId.Equals(apiSubscriptionId)));
            _logger.LogInformation(LoggingUtils.ComposeReturnValueMessage(typeof(APISubscription).Name,
               apiSubscriptionId.ToString(),
               JsonSerializer.Serialize(apiSubscription)));

            return apiSubscription;
        }

        public async Task<APISubscription> CreateAsync(APISubscription apiSubscription)
        {
            if (apiSubscription is null)
            {
                throw new LunaBadRequestUserException(LoggingUtils.ComposePayloadNotProvidedErrorMessage(typeof(APISubscription).Name),
                    UserErrorCode.PayloadNotProvided);
            }

            // Check that an offer with the same name does not already exist
            if (await ExistsAsync(apiSubscription.SubscriptionId))
            {
                throw new LunaConflictUserException(LoggingUtils.ComposeAlreadyExistsErrorMessage(typeof(APISubscription).Name,
                        apiSubscription.SubscriptionId.ToString()));
            }
            _logger.LogInformation(LoggingUtils.ComposeCreateResourceMessage(typeof(APISubscription).Name, apiSubscription.SubscriptionId.ToString(), payload: JsonSerializer.Serialize(apiSubscription)));

            // Update the product created time
            apiSubscription.CreatedTime = DateTime.UtcNow;

            // Update the product last updated time
            apiSubscription.LastUpdatedTime = apiSubscription.CreatedTime;

            // Update productDb values and save changes in APIM
            var apiSubscriptionAPIM = await _apiSubscriptionAPIM.CreateAsync(apiSubscription);

            // Update the offer last updated time
            apiSubscription.PrimaryKey = apiSubscriptionAPIM.properties.primaryKey;

            // Update the offer last updated time
            apiSubscription.SecondaryKey = apiSubscriptionAPIM.properties.secondaryKey;

            // Add product to db
            _context.APISubscriptions.Add(apiSubscription);
            await _context._SaveChangesAsync();
            _logger.LogInformation(LoggingUtils.ComposeResourceCreatedMessage(typeof(APISubscription).Name, apiSubscription.SubscriptionId.ToString()));

            return apiSubscription;
        }

        public async Task<APISubscription> UpdateAsync(Guid apiSubscriptionId, APISubscription apiSubscription)
        {
            if (apiSubscription is null)
            {
                throw new LunaBadRequestUserException(LoggingUtils.ComposePayloadNotProvidedErrorMessage(typeof(APISubscription).Name),
                    UserErrorCode.PayloadNotProvided);
            }
            _logger.LogInformation(LoggingUtils.ComposeUpdateResourceMessage(typeof(APISubscription).Name, apiSubscription.SubscriptionId.ToString(), payload: JsonSerializer.Serialize(apiSubscription)));

            // Get the offer that matches the offerName provided
            var apiSubscriptionDb = await GetAsync(apiSubscriptionId);

            // Check if (the offerName has been updated) && 
            //          (an offer with the same new name does not already exist)
            if ((!apiSubscriptionId.Equals(apiSubscription.SubscriptionId)) && (await ExistsAsync(apiSubscription.SubscriptionId)))
            {
                throw new LunaBadRequestUserException(LoggingUtils.ComposeNameMismatchErrorMessage(typeof(APISubscription).Name),
                    UserErrorCode.NameMismatch);
            }

            // Copy over the changes
            apiSubscriptionDb.Status = apiSubscription.Status;

            // Update the offer last updated time
            apiSubscriptionDb.LastUpdatedTime = DateTime.UtcNow;

            // Update productDb values and save changes in APIM
            var apiSubscriptionAPIM = await _apiSubscriptionAPIM.UpdateAsync(apiSubscriptionDb);

            // Update the offer last updated time
            apiSubscriptionDb.PrimaryKey = apiSubscriptionAPIM.properties.primaryKey;

            // Update the offer last updated time
            apiSubscriptionDb.SecondaryKey = apiSubscriptionAPIM.properties.secondaryKey;

            // Update productDb values and save changes in db
            _context.APISubscriptions.Update(apiSubscriptionDb);
            await _context._SaveChangesAsync();
            _logger.LogInformation(LoggingUtils.ComposeResourceUpdatedMessage(typeof(Product).Name, apiSubscription.SubscriptionId.ToString()));

            return apiSubscriptionDb;
        }

        public async Task<APISubscription> DeleteAsync(Guid apiSubscriptionId)
        {
            _logger.LogInformation(LoggingUtils.ComposeDeleteResourceMessage(typeof(Product).Name, apiSubscriptionId.ToString()));

            // Get the offer that matches the offerName provide
            var apiSubscription = await GetAsync(apiSubscriptionId);

            // remove the product from the APIM
            await _apiSubscriptionAPIM.DeleteAsync(apiSubscription);

            // Remove the product from the db
            _context.APISubscriptions.Remove(apiSubscription);
            await _context._SaveChangesAsync();
            _logger.LogInformation(LoggingUtils.ComposeResourceDeletedMessage(typeof(Product).Name, apiSubscriptionId.ToString()));

            return apiSubscription;
        }

        /// <summary>
        /// Regenerate key for the subscription
        /// </summary>
        /// <param name="apiSubscriptionId">subscription id</param>
        /// <param name="keyName">The key name</param>
        /// <returns>The subscription with regenerated key</returns>
        public async Task<APISubscription> RegenerateKey(Guid apiSubscriptionId, string keyName)
        {
            if (!await ExistsAsync(apiSubscriptionId))
            {
                throw new LunaNotFoundUserException(LoggingUtils.ComposeNotFoundErrorMessage(typeof(APISubscription).Name,
                    apiSubscriptionId.ToString()));
            }
            _logger.LogInformation(LoggingUtils.ComposeGetSingleResourceMessage(typeof(APISubscription).Name, apiSubscriptionId.ToString()));

            // Get the product that matches the provided productName
            var apiSubscription = await _context.APISubscriptions.SingleOrDefaultAsync(o => (o.SubscriptionId.Equals(apiSubscriptionId)));
            _logger.LogInformation(LoggingUtils.ComposeReturnValueMessage(typeof(APISubscription).Name,
               apiSubscriptionId.ToString(),
               JsonSerializer.Serialize(apiSubscription)));

            var apiSubscriptionProperties = await _apiSubscriptionAPIM.RegenerateKey(apiSubscriptionId, keyName);
            apiSubscription.PrimaryKey = apiSubscriptionProperties.primaryKey;
            apiSubscription.SecondaryKey = apiSubscriptionProperties.secondaryKey;

            // Update productDb values and save changes in db
            _context.APISubscriptions.Update(apiSubscription);
            await _context._SaveChangesAsync();
            _logger.LogInformation(LoggingUtils.ComposeResourceUpdatedMessage(typeof(APISubscription).Name, apiSubscription.SubscriptionId.ToString()));

            return apiSubscription;
        }

        public async Task<bool> ExistsAsync(Guid apiSubscriptionId)
        {
            _logger.LogInformation(LoggingUtils.ComposeCheckResourceExistsMessage(typeof(APISubscription).Name, apiSubscriptionId.ToString()));

            // Check that only one offer with this offerName exists and has not been deleted
            var count = await _context.APISubscriptions
                .CountAsync(s => (s.SubscriptionId.Equals(apiSubscriptionId)));

            // More than one instance of an object with the same name exists, this should not happen
            if (count > 1)
            {
                throw new NotSupportedException(LoggingUtils.ComposeFoundDuplicatesErrorMessage(typeof(APISubscription).Name, apiSubscriptionId.ToString()));

            }
            else if (count == 0)
            {
                _logger.LogInformation(LoggingUtils.ComposeResourceExistsOrNotMessage(typeof(APISubscription).Name, apiSubscriptionId.ToString(), false));
                return false;
            }
            else
            {
                _logger.LogInformation(LoggingUtils.ComposeResourceExistsOrNotMessage(typeof(Product).Name, apiSubscriptionId.ToString(), true));
                // count = 1
                return true;
            }
        }        

    }
}