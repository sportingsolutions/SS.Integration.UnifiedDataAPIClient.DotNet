using System.Collections;
using System.Collections.Generic;
using System.Data;

namespace SportingSolutions.Udapi.Sdk
{
    public class DataSetToIEnumerable
    {
        private readonly List<TableRow> _result = new List<TableRow>();

        public IEnumerable ConvertDataSetToEnumerable(DataSet ds)
        {
            LoadTable(ds);

            return _result;
        }

        private void LoadTable(DataSet ds)
        {
            DataTable dt = ds.Tables[0];

            foreach (var row in dt.AsEnumerable())
            {
                var rowData = new TableRow
                                  {
                                      ValorColunaA = (int) row[0], 
                                      ValorColunaB = (int) row[1],
                                      //etc
                                  };
                
                _result.Add(rowData);
            }
        }
    }

    public class TableRow
    {
        public int ValorColunaA;
        public int ValorColunaB;
        //etc
    }
}