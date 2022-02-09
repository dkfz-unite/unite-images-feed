﻿using System.Collections.Generic;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Unite.Data.Services;
using Unite.Data.Services.Configuration.Options;
using Unite.Images.Feed.Data;
using Unite.Images.Feed.Web.Configuration.Options;
using Unite.Images.Feed.Web.Handlers;
using Unite.Images.Feed.Web.HostedServices;
using Unite.Images.Feed.Web.Models.Images;
using Unite.Images.Feed.Web.Models.Images.Validators;
using Unite.Images.Feed.Web.Services;
using Unite.Images.Feed.Web.Services.Validation;
using Unite.Images.Indices.Services;
using Unite.Indices.Entities.Images;
using Unite.Indices.Services;
using Unite.Indices.Services.Configuration.Options;

namespace Unite.Images.Feed.Web.Configuration.Extensions
{
    public static class ConfigurationExtensions
    {
        public static void Configure(this IServiceCollection services)
        {
            services.AddTransient<ISqlOptions, SqlOptions>();
            services.AddTransient<IElasticOptions, ElasticOptions>();
            services.AddTransient<IndexingOptions>();

            services.AddTransient<IValidationService, ValidationService>();
            services.AddTransient<IValidator<IEnumerable<ImageModel>>, ImageModelsValidator>();

            services.AddTransient<DomainDbContext>();
            services.AddTransient<ImageDataWriter>();

            services.AddTransient<ImageIndexingTasksService>();
            services.AddTransient<TasksProcessingService>();

            services.AddHostedService<IndexingHostedService>();
            services.AddTransient<IndexingHandler>();
            services.AddTransient<IIndexCreationService<ImageIndex>, ImageIndexCreationService>();
            services.AddTransient<IIndexingService<ImageIndex>, ImagesIndexingService>();
        }
    }
}