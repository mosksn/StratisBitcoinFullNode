﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using McMaster.Extensions.CommandLineUtils;
using Stratis.SmartContracts.Core.Compilation;
using Stratis.SmartContracts.Core.ContractValidation;
using Stratis.SmartContracts.Tools.Sct.Report;
using Stratis.SmartContracts.Tools.Sct.Report.Sections;

namespace Stratis.SmartContracts.Tools.Sct.Validation
{
    [Command(Description = "Validates smart contracts for structure and determinism")]
    [HelpOption]
    class Validator
    {
        [Argument(0, Description = "The paths of the files to validate",
            Name = "[FILES]")]
        public List<string> InputFiles { get; }

        [Option("-sb|--showbytes", CommandOptionType.NoValue,
            Description = "Show contract compilation bytes")]
        public bool ShowBytes { get; }

        private int OnExecute(CommandLineApplication app)
        {
            if (!this.InputFiles.Any())
            {
                app.ShowHelp();
                return 1;
            }

            Console.WriteLine("Smart Contract Validator");

            var determinismValidator = new SmartContractDeterminismValidator();
            var formatValidator = new SmartContractFormatValidator();

            var reportData = new List<ValidationReportData>();

            foreach (string file in this.InputFiles)
            {
                if (!File.Exists(file))
                {
                    Console.WriteLine($"{file} does not exist");
                    continue;
                }

                string source;

                Console.WriteLine($"Reading {file}");

                using (var sr = new StreamReader(File.OpenRead(file)))
                {
                    source = sr.ReadToEnd();
                }

                Console.WriteLine($"Read {file} OK");

                if (string.IsNullOrWhiteSpace(source))
                {
                    Console.WriteLine($"Empty file at {file}");
                    continue;
                }

                var validationData = new ValidationReportData
                {
                    FileName = file,
                    CompilationErrors = new List<CompilationError>(),
                    DeterminismValidationErrors = new List<SmartContractValidationError>(),
                    FormatValidationErrors = new List<ValidationError>()
                };

                reportData.Add(validationData);

                Console.WriteLine($"Compiling...");
                SmartContractCompilationResult compilationResult = SmartContractCompiler.Compile(source);

                validationData.CompilationSuccess = compilationResult.Success;

                if (!compilationResult.Success)
                {
                    Console.WriteLine("Compilation failed!");

                    validationData.CompilationErrors
                        .AddRange(compilationResult
                            .Diagnostics
                            .Select(d => new CompilationError { Message = d.ToString() }));

                    continue;
                }

                validationData.CompilationBytes = compilationResult.Compilation;

                Console.WriteLine($"Compilation OK");

                byte[] compilation = compilationResult.Compilation;

                Console.WriteLine("Building ModuleDefinition");

                SmartContractDecompilation decompilation = SmartContractDecompiler.GetModuleDefinition(compilation, new DotNetCoreAssemblyResolver());

                Console.WriteLine("ModuleDefinition built successfully");

                Console.WriteLine($"Validating file {file}...");

                SmartContractValidationResult formatValidationResult = formatValidator.Validate(decompilation);

                validationData.FormatValid = formatValidationResult.IsValid;

                validationData
                    .FormatValidationErrors
                    .AddRange(formatValidationResult
                        .Errors
                        .Select(e => new ValidationError { Message = e.Message }));

                SmartContractValidationResult determinismValidationResult = determinismValidator.Validate(decompilation);

                validationData.DeterminismValid = determinismValidationResult.IsValid;

                validationData
                    .DeterminismValidationErrors
                    .AddRange(determinismValidationResult.Errors);
            }

            List<IReportSection> reportStructure = new List<IReportSection>();
            reportStructure.Add(new HeaderSection());
            reportStructure.Add(new CompilationSection());

            reportStructure.Add(new FormatSection());
            reportStructure.Add(new DeterminismSection());

            if (this.ShowBytes)
                reportStructure.Add(new ByteCodeSection());

            reportStructure.Add(new FooterSection());

            var renderer = new StreamTextRenderer(Console.Out);

            foreach (ValidationReportData data in reportData)
            {
                renderer.Render(reportStructure, data);
            }

            return 1;
        }
    }
}