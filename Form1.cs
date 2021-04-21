using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Threading;
using System.IO.Ports;          // for UART communication
using System.Data.SqlClient;    // for MySQL communication
using MySql.Data.MySqlClient;   // for MySQL communication

namespace _001_DHT11
{
    public partial class Form1 : Form
    {
        MySqlConnection mscn;
        MySqlCommand mscm;
        MySqlDataReader msdr;
        String dbServer = "127.0.0.1";
        String inData;
        String inTemperData = "0";
        String inHumiData = "0";
        String tmp1, tmp2;
        bool tmp3 = true;
        Thread th1;

        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            getAvailablePorts();

            th1 = new Thread(new ThreadStart(warningMessageBox));

            mscn = new MySqlConnection("Server=" + dbServer + ";Database=sensor_db;Uid=winuser;Pwd=p@ssw0rd;Charset=UTF8");
            try
            {
                mscn.Open();
                mscm = new MySqlCommand("", mscn);

                label5_DBStatus.Text = "Connected";
                label5_DBStatus.ForeColor = Color.Green;
            }
            catch { }

            chart1_Monitoring.ChartAreas[0].AxisX.Minimum = 0;
            chart1_Monitoring.ChartAreas[0].AxisX.Maximum = 30;
            chart1_Monitoring.ChartAreas[0].AxisY.Minimum = 0;
            chart1_Monitoring.ChartAreas[0].AxisY.Maximum = 100;

            dateTimePicker1.CustomFormat = "yyyy-MM-dd HH:mm:ss";
            dateTimePicker2.CustomFormat = "yyyy-MM-dd HH:mm:ss";

            select_Control();
        }

        private void Form1_FormClosed(object sender, FormClosedEventArgs e)
        {
            mscn.Close();
        }

        void getAvailablePorts()
        {
            String[] ports = SerialPort.GetPortNames();
            comboBox1_Port.Items.AddRange(ports);
        }

        private void button1_OpenPort_Click(object sender, EventArgs e)
        {
            try
            {
                if (comboBox1_Port.Text == "") 
                {
                    MessageBox.Show("UART COM Port Select");
                    return;
                }
                if (comboBox2_BPS.Text == "")
                {
                    MessageBox.Show("UART COM BPS Select");
                    return;
                }
                serialPort1.PortName = comboBox1_Port.Text;
                serialPort1.BaudRate = int.Parse(comboBox2_BPS.Text);
                serialPort1.Open();

                progressBar1_PortStatus.Value = 100;
                label1_ComPort.Text = "ON";
                label1_ComPort.ForeColor = Color.Green;

                button4_Send.Enabled = true;
                button2_ClosePort.Enabled = true;
                textBox2_SendData.Enabled = true;
                button1_OpenPort.Enabled = false;
                label7_Temper.Visible = true;
                label8_Humi.Visible = true;

            } 
            catch(UnauthorizedAccessException)
            {
                MessageBox.Show("UART Open Exception Error");
            }
        }

        private void button2_ClosePort_Click(object sender, EventArgs e)
        {
            serialPort1.Close();

            progressBar1_PortStatus.Value = 0;
            label1_ComPort.Text = "OFF";
            label1_ComPort.ForeColor = Color.Red;

            button4_Send.Enabled = false;
            button2_ClosePort.Enabled = false;
            textBox2_SendData.Enabled = false;
            button1_OpenPort.Enabled = true;
            label7_Temper.Visible = false;
            label8_Humi.Visible = false;

            listView1_Monitoring.Items.Clear();
        }

        private void button3_Clear_Click(object sender, EventArgs e)
        {
            richTextBox1_ReceiveData.Text = "";
        }

        private void serialPort1_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            inData = serialPort1.ReadLine();        // STM32로 부터 데이터를 읽어들임
                                                    // 115200 BPS의 경우 1 char당 약 0.1ms가 소요됨
            this.Invoke(new EventHandler(ShowData));
        }

        private void ShowData(object sender, EventArgs e)
        {
            richTextBox1_ReceiveData.AppendText(inData + '\n');

            if (inData.Contains("[Tmp]") == true)       // 온도 정보
            {
                inTemperData = inData.Split(']')[1];

                label7_Temper.Text = inTemperData;
            }
            if (inData.Contains("[Wet]") == true)       // 습도 정보
            {
                inHumiData = inData.Split(']')[1];

                label8_Humi.Text = inHumiData;
            }

            String nowTime = System.DateTime.Now.ToString("yyyy.MM.dd HH:mm:ss");
            ListViewItem lvi = new ListViewItem(new String[] { nowTime, nowTime, inTemperData, inHumiData });
            listView1_Monitoring.Items.Add(lvi);

            mscm.CommandText = "INSERT INTO sensor_tb VALUES (now(), '" + inTemperData + "', '" + inHumiData + "');";
            try
            {
                mscm.ExecuteNonQuery();

                chart1_Monitoring.Series[0].Points.Add(int.Parse(inTemperData));
                chart1_Monitoring.Series[1].Points.Add(int.Parse(inHumiData));
            }
            catch { }

            if(chart1_Monitoring.Series[0].Points.Count > 30)
            {
                chart1_Monitoring.Series[0].Points.RemoveAt(0);
                chart1_Monitoring.Series[1].Points.RemoveAt(0);
            }

            if (int.Parse(inTemperData) > int.Parse(textBox1.Text) && tmp3)
            {
                tmp3 = false;
                tmp1 = "온";
                tmp2 = "상";
                th1.Start();
            }
            else if (int.Parse(inTemperData) < int.Parse(textBox2.Text) && tmp3)
            {
                tmp3 = false;
                tmp1 = "온";
                tmp2 = "하";
                th1.Start();
            }
            else if (int.Parse(inHumiData) > int.Parse(textBox3.Text) && tmp3)
            {
                tmp3 = false;
                tmp1 = "습";
                tmp2 = "상";
                th1.Start();
            }
            else if (int.Parse(inHumiData) < int.Parse(textBox4.Text) && tmp3)
            {
                tmp3 = false;
                tmp1 = "습";
                tmp2 = "하";
                th1.Start();
            }
        }

        public void warningMessageBox()
        {
            MessageBox.Show(tmp1 + "도가 " + tmp2 + "한치를 초과했습니다.", "#### 경고 ####", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            th1.Abort();
        }

        private void button5_Select_Click(object sender, EventArgs e)
        {
            listView2_History.Items.Clear();
            chart2_History.Series[0].Points.Clear();
            chart2_History.Series[1].Points.Clear();

            mscm.CommandText = "SELECT * FROM sensor_tb WHERE s_date BETWEEN '" + dateTimePicker1.Text + "' AND '" + dateTimePicker2.Text + "';";
            msdr = mscm.ExecuteReader();

            while (msdr.Read())
            {
                String date = msdr["s_date"].ToString().Remove(11, 3);
                String temper = msdr["s_temper"].ToString();
                String humi = msdr["s_humi"].ToString();

                ListViewItem lvi = new ListViewItem(new String[] { date, date, temper, humi });
                listView2_History.Items.Add(lvi);

                chart2_History.Series[0].Points.Add(int.Parse(temper));
                chart2_History.Series[1].Points.Add(int.Parse(humi));
            }

            msdr.Close();
        }

        private void button6_temper_Click(object sender, EventArgs e)
        {
            if (mscm == null)
                return;

            mscm.CommandText = "UPDATE control_tb SET temper_max='" + textBox1.Text + "', temper_min='" + textBox2.Text + "';";
            mscm.ExecuteNonQuery();

            MessageBox.Show("적용되었습니다.");
        }

        private void button7_humi_Click(object sender, EventArgs e)
        {
            if (mscm == null)
                return;
            
            mscm.CommandText = "UPDATE control_tb SET humi_max='" + textBox3.Text + "', humi_min='" + textBox4.Text + "';";
            mscm.ExecuteNonQuery();

            MessageBox.Show("적용되었습니다.");
        }

        public void select_Control()
        {
            if (mscm == null)
                return;

            mscm.CommandText = "SELECT * FROM control_tb;";
            msdr = mscm.ExecuteReader();

            while (msdr.Read())
            {
                textBox1.Text = msdr["temper_max"].ToString();
                textBox2.Text = msdr["temper_min"].ToString();
                textBox3.Text = msdr["humi_max"].ToString();
                textBox4.Text = msdr["humi_min"].ToString();
            }

            msdr.Close();
        }

        private void button8_Exit_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private void tabPage3_Click(object sender, EventArgs e)
        {
            // 잘못 누름
        }
    }
}
