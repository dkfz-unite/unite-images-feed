﻿using Microsoft.EntityFrameworkCore;
using Unite.Data.Context;
using Unite.Data.Context.Extensions.Queryable;
using Unite.Data.Context.Repositories;
using Unite.Data.Context.Repositories.Constants;
using Unite.Data.Entities.Donors;
using Unite.Data.Entities.Donors.Clinical;
using Unite.Data.Entities.Genome.Analysis;
using Unite.Data.Entities.Genome.Transcriptomics;
using Unite.Data.Entities.Genome.Variants;
using Unite.Data.Entities.Images;
using Unite.Data.Entities.Images.Enums;
using Unite.Data.Entities.Specimens;
using Unite.Indices.Entities;
using Unite.Indices.Entities.Images;
using Unite.Mapping;

using CNV = Unite.Data.Entities.Genome.Variants.CNV;
using SSM = Unite.Data.Entities.Genome.Variants.SSM;
using SV = Unite.Data.Entities.Genome.Variants.SV;

namespace Unite.Images.Indices.Services;

public class ImageIndexCreationService
{
    private record GenomicStats(int NumberOfGenes, int NumberOfSsms, int NumberOfCnvs, int NumberOfSvs);

    private readonly IDbContextFactory<DomainDbContext> _dbContextFactory;
    private readonly DonorsRepository _donorsRepository;
    private readonly ImagesRepository _imagesRepository;
    private readonly SpecimensRepository _specimensRepository;


    public ImageIndexCreationService(IDbContextFactory<DomainDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
        _donorsRepository = new DonorsRepository(dbContextFactory);
        _imagesRepository = new ImagesRepository(dbContextFactory);
        _specimensRepository = new SpecimensRepository(dbContextFactory);
    }


    public ImageIndex CreateIndex(object key)
    {
        var imageId = (int)key;

        return CreateImageIndex(imageId);
    }


    private ImageIndex CreateImageIndex(int imageId)
    {
        var image = LoadImage(imageId);

        if (image == null)
        {
            return null;
        }

        var index = CreateImageIndex(image);

        return index;
    }

    private ImageIndex CreateImageIndex(Image image)
    {
        var diagnosisDate = image.Donor.ClinicalData?.DiagnosisDate;
        var stats = LoadGenomicStats(image.DonorId);

        var index = new ImageIndex();

        ImageIndexMapper.Map(image, index, diagnosisDate);

        index.DonorId = image.DonorId;

        index.Donor = CreateDonorIndex(image.DonorId);
        index.Specimens = CreateSpecimenIndices(image.DonorId, diagnosisDate);
        index.Data = CreateDataIndex(image.TypeId, image.DonorId);

        index.NumberOfGenes = stats.NumberOfGenes;
        index.NumberOfSsms = stats.NumberOfSsms;
        index.NumberOfCnvs = stats.NumberOfCnvs;
        index.NumberOfSvs = stats.NumberOfSvs;

        return index;
    }

    private Image LoadImage(int imageId)
    {
        using var dbContext = _dbContextFactory.CreateDbContext();

        return dbContext.Set<Image>()
            .AsNoTracking()
            .Include(image => image.MriImage)
            .Include(image => image.Donor)
                .ThenInclude(donor => donor.ClinicalData)
            .FirstOrDefault(image => image.Id == imageId);
    }


    private DonorIndex CreateDonorIndex(int donorId)
    {
        var donor = LoadDonor(donorId);

        if (donor == null)
        {
            return null;
        }

        return CreateDonorIndex(donor);
    }

    private static DonorIndex CreateDonorIndex(Donor donor)
    {
        var index = new DonorIndex();

        DonorIndexMapper.Map(donor, index);

        return index;
    }

    private Donor LoadDonor(int donorId)
    {
        using var dbContext = _dbContextFactory.CreateDbContext();

        return dbContext.Set<Donor>()
            .AsNoTracking()
            .IncludeClinicalData()
            .IncludeTreatments()
            .IncludeStudies()
            .IncludeProjects()
            .FirstOrDefault(donor => donor.Id == donorId);
    }


    private SpecimenIndex[] CreateSpecimenIndices(int donorId, DateOnly? diagnosisDate)
    {
        var specimens = LoadSpecimens(donorId);

        var indices = specimens.Select(specimen => CreateSpecimenIndex(specimen, diagnosisDate));

        return indices.Any() ? indices.ToArray() : null;
    }

    private SpecimenIndex CreateSpecimenIndex(Specimen specimen, DateOnly? diagnosisDate)
    {
        var index = new SpecimenIndex();

        SpecimenIndexMapper.Map(specimen, index, diagnosisDate);

        index.Analyses = CreateAnalysisIndices(specimen.Id, diagnosisDate);

        return index;
    }

    private Specimen[] LoadSpecimens(int donorId)
    {
        using var dbContext = _dbContextFactory.CreateDbContext();

        return dbContext.Set<Specimen>()
            .AsNoTracking()
            .IncludeMaterial()
            .IncludeMolecularData()
            .Where(specimen => specimen.DonorId == donorId)
            .Where(Predicates.IsImageRelatedSpecimen)
            .ToArray();
    }


    private AnalysisIndex[] CreateAnalysisIndices(int specimenId, DateOnly? diagnosisDate)
    {
        var analyses = LoadAnalyses(specimenId);

        var indices = analyses.Select(analysis => CreateAnalysisIndex(analysis, diagnosisDate));

        return indices.Any() ? indices.ToArray() : null;
    }

    private static AnalysisIndex CreateAnalysisIndex(AnalysedSample analysis, DateOnly? diagnosisDate)
    {
        return AnalysisIndexMapper.CreateFrom<AnalysisIndex>(analysis, diagnosisDate);
    }

    private AnalysedSample[] LoadAnalyses(int specimenId)
    {
        using var dbContext = _dbContextFactory.CreateDbContext();

        return dbContext.Set<AnalysedSample>()
            .AsNoTracking()
            .Include(analysis => analysis.Analysis)
            .Where(analysis => analysis.TargetSampleId == specimenId)
            .ToArray();
    }


    private DataIndex CreateDataIndex(ImageType type, int donorId)
    {
        using var dbContext = _dbContextFactory.CreateDbContext();

        var specimenIds = _donorsRepository.GetRelatedSpecimens([donorId]).Result;

        var index = new DataIndex();

        index.Donors = true;

        index.Clinical = dbContext.Set<ClinicalData>()
            .AsNoTracking()
            .Where(entity => entity.DonorId == donorId)
            .Any();

        index.Treatments = dbContext.Set<Treatment>()
            .AsNoTracking()
            .Where(entity => entity.DonorId == donorId)
            .Any();

        index.Mris = type == ImageType.MRI;

        index.Cts = type == ImageType.CT;

        index.Materials = dbContext.Set<Specimen>()
            .AsNoTracking()
            .Where(Predicates.IsImageRelatedSpecimen)
            .Where(entity => entity.DonorId == donorId)
            .Any();

        index.MaterialsMolecular = dbContext.Set<Specimen>()
            .AsNoTracking()
            .Include(entity => entity.MolecularData)
            .Where(Predicates.IsImageRelatedSpecimen)
            .Where(entity => entity.DonorId == donorId)
            .Where(entity => entity.MolecularData != null)
            .Any();

        index.Ssms = CheckVariants<SSM.Variant, SSM.VariantEntry>(specimenIds);

        index.Cnvs = CheckVariants<CNV.Variant, CNV.VariantEntry>(specimenIds);

        index.Svs = CheckVariants<SV.Variant, SV.VariantEntry>(specimenIds);

        index.GeneExp = CheckGeneExp(specimenIds);

        index.GeneExpSc = false;

        return index;
    }

    private GenomicStats LoadGenomicStats(int donorId)
    {
        var specimenIds = _donorsRepository.GetRelatedSpecimens([donorId]).Result;
        var ssmIds = _specimensRepository.GetRelatedVariants<SSM.Variant>(specimenIds).Result;
        var cnvIds = _specimensRepository.GetRelatedVariants<CNV.Variant>(specimenIds).Result;
        var svIds = _specimensRepository.GetRelatedVariants<SV.Variant>(specimenIds).Result;
        var geneIds = _specimensRepository.GetVariantRelatedGenes(specimenIds).Result;

        return new GenomicStats(geneIds.Length, ssmIds.Length, cnvIds.Length, svIds.Length);
    }


    /// <summary>
    /// Checks if variants data of given type is available for given specimens.
    /// </summary>
    /// <param name="specimenIds">Specimen identifiers.</param>
    /// <typeparam name="TVariant">Variant type.</typeparam>
    /// <typeparam name="TVariantEntry">Variant occurrence type.</typeparam>
    /// <returns>'true' if variants data exists or 'false' otherwise.</returns>
    private bool CheckVariants<TVariant, TVariantEntry>(int[] specimenIds)
        where TVariant : Variant
        where TVariantEntry : VariantEntry<TVariant>
    {
        using var dbContext = _dbContextFactory.CreateDbContext();

        return dbContext.Set<TVariantEntry>()
            .AsNoTracking()
            .Where(entry => specimenIds.Contains(entry.AnalysedSample.TargetSampleId))
            .Select(entry => entry.EntityId)
            .Distinct()
            .Any();
    }

    /// <summary>
    /// Checks if gene expression data is available for given specimens.
    /// </summary>
    /// <param name="specimenIds">Specimen identifiers.</param>
    /// <returns>'true' if gene expression data exists or 'false' otherwise.</returns>
    private bool CheckGeneExp(int[] specimenIds)
    {
        using var dbContext = _dbContextFactory.CreateDbContext();

        return dbContext.Set<BulkExpression>()
            .AsNoTracking()
            .Any(expression => specimenIds.Contains(expression.AnalysedSample.TargetSampleId));
    }
}
