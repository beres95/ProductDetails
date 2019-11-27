using System;
using System.Linq;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using HHTCe.HHT;
using System.Data.SqlClient;

namespace HHTCe
{
    public partial class frmProductDetails : Form
    {
        private DeviceConfig _config;
        private Scanner _scanner;
        private string _freeStock;
        private string ipAddress;



        public frmProductDetails()
        {
            InitializeComponent();
        }

#region Methods

        private void frmProductDetails_Load(object sender, EventArgs e)
        {
            try
            {
                _config = DeviceConfig.GetDeviceConfig();
            }
            catch (Exception exn)
            {
                MessageBox.Show("Could not load device config. " + exn.Message, "HHTCe Error");
                this.Close();
                return;
            }

            try
            {
                Cursor.Current = Cursors.WaitCursor;

                // Check if wireless is disabled or a connection cannot be established
                if (!_config.WirelessEnabled || !Network.TestConnection())
                {
                    MessageBox.Show("ERROR: Device is not connected to the internet or cannot connect to SQL.");
                    this.Dispose();  
                    return;
                    
                }
            }
            finally
            {
                Cursor.Current = Cursors.Default;
            }


            // Load scanner.
            _scanner = new Scanner(true);
            _scanner.StartRead();
            _scanner.AfterScan += ScanBarcode;
            textBoxBarcode.Focus();
            textBoxBarcode.SelectAll();
        }

        private void ScanBarcode(string barcode)
        {
            if (!textBoxBarcode.Focused)
            {
                return;
            }

            textBoxBarcode.Text = barcode;
            ProductDetails();
            
        }

        private void ProductDetails()
        {
            try
            {

                Cursor.Current = Cursors.WaitCursor;
                Application.DoEvents();

                string scannedBarcode = textBoxBarcode.Text.Trim();

                if (string.IsNullOrEmpty(scannedBarcode))
                {
                    throw new Exception("No Barcode was provided.");
                    textBoxBarcode.Focus();
                }

                //clearing

                Clear();


                // assigning product details from scanned barcode to variable prod
                Product prod = new Product(scannedBarcode);

                // getting variable prod's stock
                StockQuantityReplen(prod.ProductCode, prod.ProductVar, GlobalVariables.Config.LocationCode);

                //getting prod attributes
                ProductAtts(prod.ProductCode, prod.ProductVar);

                //getting prices
                PriceHistory(prod.ProductCode, prod.ProductVar);
                FuturePrice(prod.ProductCode, prod.ProductVar);

                //getting promos
                ProductPromos(prod.ProductCode, prod.ProductVar);

                //getting hierarchy + unit sizes
                ProductHierarchy(prod.ProductCode, prod.ProductVar);

                //gets printers and label types
                getLabelTypes();
                getPrinters();

                
                

                
                // displays info on for product tab


                lblProdCodeResult.Text = prod.ProductCode.ToString() + " / " + prod.ProductVar.ToString();
                lblPriceResult.Text = prod.SellPrice.ToString();
                lblPricingCurrentResult.Text = prod.SellPrice.ToString();
                lblFreeStockResult.Text = _freeStock;
                lblSizeColourResult.Text = prod.SizeColour;
                lblDescriptionResult.Text = prod.ProductDescription.ToString();
                


            }

            catch (Exception exn)
            {
                MessageBox.Show("ERROR: " + exn.Message);
                textBoxBarcode.Focus();
                return;
            }
            finally
            {
                Cursor.Current = Cursors.Default;
            }

            textBoxBarcode.Focus();
            textBoxBarcode.SelectAll();


        } // shows all the info

        private void StockQuantityReplen(string ProductCode, string ProductVar, string LocCode) // runs a query to assign stock from a product to _quantity
        {
            string query = @"SELECT 
                                i_stk_bal, i_rep_meth, i_rep_source, br_loc_max_lev, br_loc_min_lev
                             FROM Streetwise_Live.dbo.brprod pr
                             WHERE
                                i_loc_code = @LocCode
                             AND
                                i_prod_code= @ProductCode
                             AND
                                i_promo = @ProductVar";

            DataTable dtProduct = null;

            List<SqlParameter> parameters = new List<SqlParameter>()
            {
                new SqlParameter("@LocCode", SqlDbType.Char, 5) {Value = LocCode},
                new SqlParameter("@ProductCode", SqlDbType.Char, 16) {Value = ProductCode},
                new SqlParameter("@ProductVar", SqlDbType.Char, 1) {Value = ProductVar}
            };

            try
            {
                dtProduct = Network.RunExternalSqlQuery(Network.ConnectionString_Streetwise, query, parameters);
            }
            catch (Exception exn)
            {
                throw exn;
            }

            if (dtProduct.Rows.Count > 0)
            {
                _freeStock = dtProduct.Rows[0]["i_stk_bal"].ToString().Trim(); // assigning stock

                //replen tab
                lblSourceResult.Text = dtProduct.Rows[0]["i_rep_source"].ToString();
                lblMinResult.Text = dtProduct.Rows[0]["br_loc_min_lev"].ToString().Trim();
                lblMaxResult.Text = dtProduct.Rows[0]["br_loc_max_lev"].ToString().Trim();

                int i = Convert.ToInt32(dtProduct.Rows[0]["i_rep_meth"]);

                if (i == 7)      
                {
                    lblReplenResult.Text = "7 - By Stocking Levels";
                }

                if (i == 8)
                {
                    lblReplenResult.Text = "8 - Suspend";
                }

                
            }
                
            
        
        }   

        private void Clear()
        {
            listBoxAtt.Items.Clear();
            dataGridPriceHistory.DataSource = null;
            comboBoxPrinter.Items.Clear();
            comboBoxType.Items.Clear();
        }  // empties datagrid and datalist etc before each scan

        private void ProductAtts(string ProductCode, string ProductVar)
        {
            string query = @"SELECT distinct
                                ac.i_att_cat, 
                                ac.i_att_cat_desc,
                                ap.i_prod_code, 
                                i_promo
                             FROM Streetwise_Live.dbo.attcat ac
                             JOIN attprod ap (nolock) on ac.i_att_cat = ap.i_att_cat 
                             JOIN brprod br (nolock) on ap.i_prod_code = br.i_prod_code                                   
                             WHERE                             
                                ap.i_prod_code= @ProductCode
                             AND
                                i_promo = @ProductVar
                             Order by ac.i_att_cat";

            DataTable dtProduct = null;

            List<SqlParameter> parameters = new List<SqlParameter>()
            {
                new SqlParameter("@ProductCode", SqlDbType.Char, 16) {Value = ProductCode},
                new SqlParameter("@ProductVar", SqlDbType.Char, 1) {Value = ProductVar}
            };

            try
            {
                dtProduct = Network.RunExternalSqlQuery(Network.ConnectionString_Streetwise, query, parameters);
            }
            catch (Exception exn)
            {
                throw exn;
            }

            if (dtProduct.Rows.Count > 0)
            {
                foreach (DataRow r in dtProduct.Rows)
                {
                    listBoxAtt.Items.Add(r["i_att_cat"].ToString().Trim() + " - " + r["i_att_cat_desc"]);
                }
                
            }
        } //shows product attributes

        private void PriceHistory(string ProductCode, string ProductVar)
        {
            string query = @"SELECT 
                                DateEffective, 
                                PreviousSellPrice                                 
                             FROM RoyRetailDB.dbo.tblPriceChangeHistory ph
                             WHERE SellChange = 1                                                               
                             AND                             
                                prod_code= @ProductCode
                             AND
                                prod_var = @ProductVar
                             ORDER BY DateEffective desc";

            DataTable dtProduct = null;

            List<SqlParameter> parameters = new List<SqlParameter>()
            {
                new SqlParameter("@ProductCode", SqlDbType.Char, 16) {Value = ProductCode},
                new SqlParameter("@ProductVar", SqlDbType.Char, 1) {Value = ProductVar}
            };

            try
            {
                dtProduct = Network.RunExternalSqlQuery(Network.ConnectionString_Streetwise, query, parameters);
            }
            catch (Exception exn)
            {
                throw exn;
            }

            if (dtProduct.Rows.Count > 0)
            {
                dataGridPriceHistory.DataSource = dtProduct;
            }
        } // shows price history

        private void FuturePrice(string ProductCode, string ProductVar)
        {
            string query = @"SELECT 
                                DateEffective, 
                                SellPrice                                 
                             FROM RoyRetailDB.dbo.tblPriceChangeQueue pq
                             WHERE SellChange = 1
                             AND DateEffective > GETDATE() AND DateEffective < GETDATE()+7                                                              
                             AND                             
                                prod_code= @ProductCode
                             AND
                                prod_var = @ProductVar
                             ORDER BY DateEffective";
                             

            DataTable dtProduct = null;

            List<SqlParameter> parameters = new List<SqlParameter>()
            {
                new SqlParameter("@ProductCode", SqlDbType.Char, 16) {Value = ProductCode},
                new SqlParameter("@ProductVar", SqlDbType.Char, 1) {Value = ProductVar}
            };

            try
            {
                dtProduct = Network.RunExternalSqlQuery(Network.ConnectionString_Streetwise, query, parameters);
            }
            catch (Exception exn)
            {
                throw exn;
            }

            if (dtProduct.Rows.Count > 0)
            {
                DateTime priceDate = Convert.ToDateTime(dtProduct.Rows[0]["DateEffective"].ToString().Trim());
                lblFuturePriceResult.Text = dtProduct.Rows[0]["SellPrice"].ToString().Trim() + "\n" +priceDate.ToString("dd/MM/yyyy");

            }
            else
            {
                lblFuturePriceResult.Text = "-";
            }
        } //shows any price changes in next 7 days

        private void ProductPromos(string ProductCode, string ProductVar)
        {
            string query = @"SELECT 
                                ph.i_promo_desc, 
                                i_promo_st_date,
                                i_promo_end_date                                 
                             FROM Streetwise_Live.dbo.pritm pi
                             JOIN prhdr ph (nolock)on pi.i_promo_id = ph.i_promo_id                                                                                              
                             WHERE                             
                                i_prod_code = @ProductCode
                             AND
                                i_promo = @ProductVar
                             AND GETDATE() between i_promo_st_date and i_promo_end_date";
                            

            DataTable dtProduct = null;

            List<SqlParameter> parameters = new List<SqlParameter>()
            {
                new SqlParameter("@ProductCode", SqlDbType.Char, 16) {Value = ProductCode},
                new SqlParameter("@ProductVar", SqlDbType.Char, 1) {Value = ProductVar}
            };

            try
            {
                dtProduct = Network.RunExternalSqlQuery(Network.ConnectionString_Streetwise, query, parameters);
            }
            catch (Exception exn)
            {
                throw exn;
            }

            if (dtProduct.Rows.Count > 0)
            {
                lblDescResult.Text = dtProduct.Rows[0]["i_promo_desc"].ToString().Trim();
                DateTime startDate = Convert.ToDateTime(dtProduct.Rows[0]["i_promo_st_date"].ToString().Trim());
                DateTime endDate = Convert.ToDateTime(dtProduct.Rows[0]["i_promo_end_date"].ToString().Trim());

                lblStartResult.Text = startDate.ToString("dd/MM/yyyy");
                lblEndResult.Text = endDate.ToString("dd/MM/yyyy");
                
                
            }

            else
            {
                lblDescResult.Text = "No promos to show";
                lblStartResult.Text = "-";
                lblEndResult.Text = "-";
            }
        } //displays current promotions

        private void ProductHierarchy(string ProductCode, string ProductVar)
        {
            string query = @"SELECT hi.i_dept + ' - ' + i_dept_desc as dept, hi.i_group + ' - ' + i_group_desc as i_group, hi.i_sub_grp_no + ' - ' + i_sub_grp_desc as sub, p.i_prod_code, p.i_wh_unit, p.i_supp_unit
                             FROM Streetwise_Live.dbo.Vw_Hierarchy(nolock) hi
                             join Streetwise_Live.dbo.prods (nolock) p on hi.i_dept = p.i_dept
                             and hi.i_group = p.i_group
                             and hi.i_sub_grp_no = p.i_sub_grp_no                                                               
                             WHERE                             
                                i_prod_code= @ProductCode
                             AND
                                i_promo = @ProductVar";
                             

            DataTable dtProduct = null;

            List<SqlParameter> parameters = new List<SqlParameter>()
            {
                new SqlParameter("@ProductCode", SqlDbType.Char, 16) {Value = ProductCode},
                new SqlParameter("@ProductVar", SqlDbType.Char, 1) {Value = ProductVar}
            };

            try
            {
                dtProduct = Network.RunExternalSqlQuery(Network.ConnectionString_Streetwise, query, parameters);
            }
            catch (SqlException sqlxn)
            {
                throw sqlxn;
            }

            if (dtProduct.Rows.Count > 0)
            {
                foreach (DataRow r in dtProduct.Rows)
                {
                    lblDeptResult.Text = r["dept"].ToString();
                    lblGroupResult.Text = r["i_group"].ToString();
                    lblSubGroupResult.Text = r["sub"].ToString();
                    lblWhSizeResult.Text = r["i_wh_unit"].ToString().Trim();
                    lblSupplierSizeResult.Text = r["i_supp_unit"].ToString().Trim();
                }	 
            }
        }
        
        private void getPrinters()
        {
            string query = @"SELECT printer_name, printer_ip                                 
                             FROM RoyRetailDB.dbo.tblPrinters
                             WHERE loc_code = @loc_code
                             AND OnGuns = 'Yes'                             
                             ORDER BY printer_name"; //change OnGuns status in tblprinters to Yes, to add printers to guns

            DataTable dtPrinters = null;

            List<SqlParameter> parameters = new List<SqlParameter>()
            {
                new SqlParameter("@loc_code", SqlDbType.VarChar, 3) {Value = _config.LocationCode}                
            };

            try
            {
                dtPrinters = Network.RunExternalSqlQuery(Network.ConnectionString_Streetwise, query, parameters);
            }
            catch (Exception exn)
            {
                throw exn;
            }

            if (dtPrinters.Rows.Count > 0)
            {
                foreach (DataRow row in dtPrinters.Rows)
                {
                    comboBoxPrinter.Items.Add(row["printer_name"]);
                }

            }
        }

        private void getLabelTypes()
        {
            string query = @"SELECT Description                                 
                             FROM RoyRetailDB.dbo.tblLabels_Types lt (nolock)
                             JOIN RoyRetailDB.dbo.tblLabels_TypeLoc_Link ltl (nolock) on lt.Label_ID=ltl.Label_ID
                             WHERE loc_code = @loc_code                             
                             ORDER BY lt.Label_ID";

            DataTable dtLabels = null;

            List<SqlParameter> parameters = new List<SqlParameter>()
            {
                new SqlParameter("@loc_code", SqlDbType.VarChar, 3) {Value = _config.LocationCode}                
            };

            try
            {
                dtLabels = Network.RunExternalSqlQuery(Network.ConnectionString_Streetwise, query, parameters);
            }
            catch (Exception exn)
            {
                throw exn;
            }

            if (dtLabels.Rows.Count > 0)
            {
                foreach (DataRow row in dtLabels.Rows)
                {
                    comboBoxType.Items.Add(row["Description"]);
                }

            }
        }

        private void getIP()
        {
            string query = @"SELECT printer_ip                                 
                             FROM RoyRetailDB.dbo.tblPrinters
                             WHERE printer_name = @printer_name";

            DataTable dtPrinters = null;

            List<SqlParameter> parameters = new List<SqlParameter>()
            {
                new SqlParameter("@printer_name", SqlDbType.VarChar, 50) {Value = comboBoxPrinter.SelectedItem.ToString()}                
            };

            try
            {
                dtPrinters = Network.RunExternalSqlQuery(Network.ConnectionString_Streetwise, query, parameters);
            }
            catch (Exception exn)
            {
                throw exn;
            }

            if (dtPrinters.Rows.Count > 0)
            {
                foreach (DataRow row in dtPrinters.Rows)
                {
                    ipAddress = row["printer_ip"].ToString();
                }

            }
        }

        

        public String LabelType()
        {
            string scannedBarcode = textBoxBarcode.Text.Trim();
            Product prod = new Product(scannedBarcode);

            int xAxisBarcode = 0;
            int xAxisProdCode = 0;
            int xAxisPrice = 0;

            if (prod.ProductCode.Length == 8)
            {
                xAxisBarcode = 80;
                xAxisProdCode = 125;
                xAxisPrice = 137;
            }

            else
            {
                xAxisBarcode = 40;
                xAxisProdCode = 100;
                xAxisPrice = 125;
                
            }

            
            string[] sizecolour = prod.SizeColour.Split(' ');
            
            

            string command = "";

            string General =
                "^XA" +
                "^PW304" +
                //Set Default Font
                "^CF0,25,25" +
                //Make sure all text orientation is normal
                "^FWN" +
                //Set Label Top and Label left shift to 0
                "^LT0" +
                "^LS0" +
                //C128 is to change utf-8 code into unicode chars. Specifically £
                "^CI28" +
                //Data = price
                "^FO105,100 ^A0N,45,45 ^FH ^FD" + "_c2_a3" + prod.SellPrice + "^FS" +
                //Bar code generation
                // Data = Product Code to generate bar code
                //Posi1 - Used to automatically center barcode.
                "^FO"+ xAxisBarcode+",150 ^BY1.4,3,70 ^B3N,N,,N,N  ^FD" + prod.ProductCode + "^FS" +
                //Write product code underneath bar code
                //Posi2 - used to automaticaly center product code under barcode.
                "^FO"+ xAxisProdCode+",227 ^A0N,20,20 ^FD" + prod.ProductCode + "^FS" +
                "^XZ";

            string Description =
                "^XA" +
                "^PW304" +
                //Set Default Font
                "^CF0,30,30" +
                //Make sure all text orientation is normal
                "^FWN" +
                //Set Label Top and Label left shift to 0
                "^LT0" +
                "^LS0" +
                // Data = Description of product
                "^FO40,120 ^FB250,2,0,C,0 ^FD" + prod.ProductDescription + "^FS" +
                "^XZ";
            
            string Plant =
                "^XA" +
                "^PW304" +
                //Set Default Font
                "^CF0,25,25" +
                //Make sure all text orientation is normal
                "^FWN" +
                //Set Label Top and Label left shift to 0
                "^LT0" +
                "^LS0" +
                // Data = Description of product
                "^FO40,90 ^FB250,2,0,C,0 ^FD" + prod.ProductDescription + "^FS" +
                //Bar code generation
                // Data = Product Code to generate bar code
                //Posi0 - Used to automatically center barcode.
                "^FO" + xAxisBarcode + ",150 ^BY1.4,3,50 ^B3N,N,,N,N  ^FD" + prod.ProductCode + "^FS" +
                //Write product code underneath bar code
                //Posi1 - used to automaticaly center product code under barcode.
                "^FO" + xAxisProdCode + ",210 ^A0N,20,20 ^FD" + prod.ProductCode + "^FS" +
                //C128 is to change utf-8 code into unicode chars. Specifically £
                "^CI28" +
                //Data = price
                "^FO" + xAxisPrice + ",240 ^A0N,25,25 ^FH ^FD" + "_c2_a3" + prod.SellPrice + "^FS" +
                "^XZ";
            if (sizecolour.Length > 1)
            {
                string Fashion =
                "^XA" +
                "^PW304" +
                    //Set Default Font
                "^CF0,25,25" +
                    //Make sure all text orientation is normal
                "^FWN" +
                    //Set Label Top and Label left shift to 0
                "^LT0" +
                "^LS0" +
                    // Data = Description of product
                "^FO40,30 ^FB250,2,0,C,0 ^FD" + prod.ProductDescription + "^FS" +
                "^FO40,90 ^FDColour:^FS" +
                    // Data = Colour
                "^FO135,90 ^FD" + sizecolour[0] + "^FS" +
                "^FO40,115 ^FDSize:^FS" +
                    // Data = Size
                "^FO135,115 ^FD" + sizecolour[1] + "^FS" +
                    //Bar code generation
                    // Data = Product Code to generate bar code
                    //Posi0 - Used to automatically center barcode.
                "^FO" + xAxisBarcode + ",150 ^BY1.4,3,70 ^B3N,N,,N,N  ^FD" + prod.ProductCode + "^FS" +
                    //Write product code underneath bar code
                    //Posi1 - used to automaticaly center product code under barcode.
                "^FO" + xAxisProdCode + ",227 ^A0N,20,20 ^FD" + prod.ProductCode + "^FS" +
                    //C128 is to change utf-8 code into unicode chars. Specifically £
                "^CI28" +
                    //Data = price
                "^FO105,260 ^A0N,45,45 ^FH ^FD" + "_c2_a3" + prod.SellPrice + "^FS" +
                "^XZ";

                string FashionNP =
                    "^XA" +
                    "^PW304" +
                    //Set Default Font
                    "^CF0,25,25" +
                    //Make sure all text orientation is normal
                    "^FWN" +
                    //Set Label Top and Label left shift to 0
                    "^LT0" +
                    "^LS0" +
                    // Data = Description of product
                    "^FO40,30 ^FB250,2,0,C,0 ^FD" + prod.ProductDescription + "^FS" +
                    "^FO40,90 ^FDColour:^FS" +
                    // Data = Colour
                    "^FO135,90 ^FD" + sizecolour[0] + "^FS" +
                    "^FO40,115 ^FDSize:^FS" +
                    // Data = Size
                    "^FO135,115 ^FD" + sizecolour[1] + "^FS" +
                    //Bar code generation
                    // Data = Product Code to generate bar code
                    //Posi0 - Used to automatically center barcode.
                    "^FO" + xAxisBarcode + ",150 ^BY1.4,3,70 ^B3N,N,,N,N  ^FD" + prod.ProductCode + "^FS" +
                    //Write product code underneath bar code
                    //Posi1 - used to automaticaly center product code under barcode.
                    "^FO" + xAxisProdCode + ",227 ^A0N,20,20 ^FD" + prod.ProductCode + "^FS" +
                    "^XZ";

                if (comboBoxType.SelectedItem.ToString() == "Peel - Fashion (PD)")
                {
                    command = Fashion;
                }

                if (comboBoxType.SelectedItem.ToString() == "Peel - Fashion No Price (PD)")
                {
                    command = FashionNP;
                }
            }
            



            if (comboBoxType.SelectedItem.ToString() == "Peel - General (PD)")
            {                
                command = General;
            }

            if (comboBoxType.SelectedItem.ToString() == "Peel - Description (PD)")
            {
                command = Description; ;
            }

            if (comboBoxType.SelectedItem.ToString() == "Peel - Plant (PD)")
            {
                command = Plant;
            }

            if (comboBoxType.SelectedItem.ToString() == "Peel - Fashion (PD)" && sizecolour.Length <= 1)
            {
                MessageBox.Show("This product cannot not be printed with this label type");
                
            }

            if (comboBoxType.SelectedItem.ToString() == "Peel - Fashion No Price (PD)" && sizecolour.Length <= 1)
            {
                MessageBox.Show("This product cannot not be printed with this label type");
                
            }
          

            return command;
            
            
        }
        private void PrintLabel()
        {
                        
            // Printer IP Address and communication port
            
            
            int port = 9100;

            

            try
            {
                // Open connection
                System.Net.Sockets.TcpClient client = new System.Net.Sockets.TcpClient();
                client.Connect(ipAddress, port);

                // Write ZPL String to connection
                System.IO.StreamWriter writer =
                new System.IO.StreamWriter(client.GetStream());

                for (int i = 0; i < numUpDownQty.Value; i++)
                {
                    writer.Write(comboBoxPrinter.Text);
                    writer.Write(LabelType());
                }
                    


                writer.Flush();

                // Close Connection
                writer.Close();
                client.Close();
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        
       

#endregion


#region Button Handlers

        private void frmProductDetails_Closing(object sender, CancelEventArgs e)
        {
            _scanner.TerminateReader();
        }

        private void frmProductDetails_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                Close();
            }

            if (e.KeyCode == Keys.Enter)
            {
                ProductDetails();
            }

            if (e.KeyCode == Keys.Left)
            {                
                tabControl1.SelectedIndex--;
               
            }

            if (e.KeyCode == Keys.Right)
            {                
                tabControl1.SelectedIndex++;                
            }
        }

        private void btnBack_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void btnClear_Click(object sender, EventArgs e)
        {
            textBoxBarcode.Text = "";
            lblProdCodeResult.Text = "";
            lblPriceResult.Text = "";
            lblPricingCurrentResult.Text = "";
            lblFreeStockResult.Text = "";
            lblSizeColourResult.Text = "";
            lblDescriptionResult.Text = "";
            lblDescResult.Text = "";
            lblStartResult.Text = "";
            lblEndResult.Text = "";
            lblFuturePriceResult.Text = "";
            lblDeptResult.Text = "";
            lblGroupResult.Text = "";
            lblSubGroupResult.Text = "";
            lblReplenResult.Text = "";
            lblSourceResult.Text = "";
            lblMaxResult.Text = "";
            lblMinResult.Text = "";
            lblWhSizeResult.Text = "";
            lblSupplierSizeResult.Text = "";

            comboBoxPrinter.Items.Clear();
            comboBoxType.Items.Clear();

            listBoxAtt.Items.Clear();
            dataGridPriceHistory.DataSource = null;
            textBoxBarcode.Focus();
        }

        private void btnGo_Click(object sender, EventArgs e)
        {
            ProductDetails();
        }

        private void tabControl1_SelectedIndexChanged(object sender, EventArgs e)
        {
            textBoxBarcode.Focus();
           
        }

        private void btnPrint_Click(object sender, EventArgs e)
        {            
            if (comboBoxType.Text != "" && comboBoxPrinter.Text != "")
            {
                if (comboBoxType.Text != null && comboBoxPrinter.Text != null)
                {
                    PrintLabel();
                }

            }
        }

        private void comboBoxPrinter_SelectedValueChanged(object sender, EventArgs e)
        {
            getIP();
            
        }
#endregion

        

        

        







    }
}