using MyDeezerStats.Domain.Entities.Excel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyDeezerStats.Application.Interfaces
{
    public interface IExcelService
    {
        /// <summary>
        /// Traite un fichier Excel et importe les données d'écoute
        /// </summary>
        /// <param name="stream">Stream du fichier Excel</param>
        /// <param name="batchSize">Taille des lots pour l'insertion</param>
        /// <returns>Résultat de l'importation</returns>
        Task<ImportResult> ProcessExcelFileAsync(Stream stream, int batchSize = 1000);
    }
}
