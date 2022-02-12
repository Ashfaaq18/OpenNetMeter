#include "DeskBand.h"

#pragma data_seg("SHARED")
double down = 0;
int downSuffix = 0;
double up= 0;
int upSuffix = 0;
int color = 2; //1 = black, 2 = white (font color)
#pragma data_seg()


void SetDataVars(double d, int dS, double u, int uS)
{
    down = d;
    downSuffix = dS;
    up = u;
    upSuffix = uS;
}

void SetFontColor(int c)
{
    color = c;
}

#pragma comment(linker, "/section:SHARED,RWS")  

#define RECTWIDTH(x)   ((x).right - (x).left)
#define RECTHEIGHT(x)  ((x).bottom - (x).top)
#define TEXTSIZE        12
#define IDT_TIMER1      1001

extern long         g_cDllRef;
extern HINSTANCE    g_hInst;

extern CLSID CLSID_DeskBand;
static const WCHAR g_szDeskBandClass[] = L"ONM_DeskBand_Class";

CDeskBand::CDeskBand() :
    m_cRef(1), m_pSite(NULL), m_pInputObjectSite(NULL), m_fHasFocus(FALSE), m_fIsDirty(FALSE), m_dwBandID(0), m_hwnd(NULL), m_hwndParent(NULL)
{
    InterlockedIncrement(&g_cDllRef);
    strRecv = L"D: 0.00bps";
    strSend = L"U: 0.00bps";
}

CDeskBand::~CDeskBand()
{
    if (m_pSite)
    {
        m_pSite->Release();
    }
    if (m_pInputObjectSite)
    {
        m_pInputObjectSite->Release();
    }
    InterlockedDecrement(&g_cDllRef);
}

//
// IUnknown
//
STDMETHODIMP CDeskBand::QueryInterface(REFIID riid, void **ppv)
{
    HRESULT hr = S_OK;

    if (IsEqualIID(IID_IUnknown, riid)       ||
        IsEqualIID(IID_IOleWindow, riid)     ||
        IsEqualIID(IID_IDockingWindow, riid) ||
        IsEqualIID(IID_IDeskBand, riid)      ||
        IsEqualIID(IID_IDeskBand2, riid))
    {
        *ppv = static_cast<IOleWindow *>(this);
    }
    else if (IsEqualIID(IID_IPersist, riid) ||
             IsEqualIID(IID_IPersistStream, riid))
    {
        *ppv = static_cast<IPersist *>(this);
    }
    else if (IsEqualIID(IID_IObjectWithSite, riid))
    {
        *ppv = static_cast<IObjectWithSite *>(this);
    }
    else if (IsEqualIID(IID_IInputObject, riid))
    {
        *ppv = static_cast<IInputObject *>(this);
    }
    else
    {
        hr = E_NOINTERFACE;
        *ppv = NULL;
    }

    if (*ppv)
    {
        AddRef();
    }

    return hr;
}

STDMETHODIMP_(ULONG) CDeskBand::AddRef()
{
    return InterlockedIncrement(&m_cRef);
}

STDMETHODIMP_(ULONG) CDeskBand::Release()
{
    ULONG cRef = InterlockedDecrement(&m_cRef);
    if (0 == cRef)
    {
        delete this;
    }

    return cRef;
}

//
// IOleWindow
//
STDMETHODIMP CDeskBand::GetWindow(HWND *phwnd)
{
    *phwnd = m_hwnd;
    return S_OK;
}

STDMETHODIMP CDeskBand::ContextSensitiveHelp(BOOL)
{
    return E_NOTIMPL;
}

//
// IDockingWindow
//
STDMETHODIMP CDeskBand::ShowDW(BOOL fShow)
{
    if (m_hwnd)
    {
        ShowWindow(m_hwnd, fShow ? SW_SHOW : SW_HIDE);
    }

    return S_OK;
}

STDMETHODIMP CDeskBand::CloseDW(DWORD)
{
    if (m_hwnd)
    {
        ShowWindow(m_hwnd, SW_HIDE);
        DestroyWindow(m_hwnd);
        m_hwnd = NULL;
    }

    return S_OK;
}

STDMETHODIMP CDeskBand::ResizeBorderDW(const RECT *, IUnknown *, BOOL)
{
    return E_NOTIMPL;
}

//
// IDeskBand
//
STDMETHODIMP CDeskBand::GetBandInfo(DWORD dwBandID, DWORD, DESKBANDINFO *pdbi)
{
    HRESULT hr = E_INVALIDARG;

    if (pdbi)
    {
        m_dwBandID = dwBandID;

        if (pdbi->dwMask & DBIM_MINSIZE)
        {
            HDC hDC = GetDC(NULL);
            RECT r = { 0, 0, 0, 0 };
            std::wstring str = L"X: 0000.00xxxx";
            DrawText(hDC, str.c_str(), static_cast<int>(str.length()), &r, DT_CALCRECT | DT_NOPREFIX | DT_SINGLELINE);

            pdbi->ptMinSize.x = abs(r.right - r.left);
            pdbi->ptMinSize.y = abs(r.bottom - r.top) * 2;
        }

        if (pdbi->dwMask & DBIM_MAXSIZE)
        {
            pdbi->ptMaxSize.y = -1;
        }

        if (pdbi->dwMask & DBIM_INTEGRAL)
        {
            pdbi->ptIntegral.y = 1;
        }

        if (pdbi->dwMask & DBIM_ACTUAL)
        {
            pdbi->ptActual.x = -1;
            pdbi->ptActual.y = -1;
        }

        if (pdbi->dwMask & DBIM_TITLE)
        {
            // Don't show title by removing this flag.
            pdbi->dwMask &= ~DBIM_TITLE;
        }

        if (pdbi->dwMask & DBIM_MODEFLAGS)
        {
            pdbi->dwModeFlags = DBIMF_NORMAL | DBIMF_VARIABLEHEIGHT;
        }

        if (pdbi->dwMask & DBIM_BKCOLOR)
        {
            // Use the default background color by removing this flag.
            pdbi->dwMask &= ~DBIM_BKCOLOR;
        }

        hr = S_OK;
    }

    return hr;
}

//
// IDeskBand2
//
STDMETHODIMP CDeskBand::CanRenderComposited(BOOL *pfCanRenderComposited)
{
    *pfCanRenderComposited = TRUE;

    return S_OK;
}

STDMETHODIMP CDeskBand::SetCompositionState(BOOL fCompositionEnabled)
{
    m_fCompositionEnabled = fCompositionEnabled;

    InvalidateRect(m_hwnd, NULL, TRUE);
    UpdateWindow(m_hwnd);

    return S_OK;
}

STDMETHODIMP CDeskBand::GetCompositionState(BOOL *pfCompositionEnabled)
{
    *pfCompositionEnabled = m_fCompositionEnabled;

    return S_OK;
}

//
// IPersist
//
STDMETHODIMP CDeskBand::GetClassID(CLSID *pclsid)
{
    *pclsid = CLSID_DeskBand;
    return S_OK;
}

//
// IPersistStream
//
STDMETHODIMP CDeskBand::IsDirty()
{
    return m_fIsDirty ? S_OK : S_FALSE;
}

STDMETHODIMP CDeskBand::Load(IStream * /*pStm*/)
{
    return S_OK;
}

STDMETHODIMP CDeskBand::Save(IStream * /*pStm*/, BOOL fClearDirty)
{
    if (fClearDirty)
    {
        m_fIsDirty = FALSE;
    }

    return S_OK;
}

STDMETHODIMP CDeskBand::GetSizeMax(ULARGE_INTEGER * /*pcbSize*/)
{
    return E_NOTIMPL;
}

//
// IObjectWithSite
//
STDMETHODIMP CDeskBand::SetSite(IUnknown *pUnkSite)
{
    HRESULT hr = S_OK;

    m_hwndParent = NULL;

    if (m_pSite)
    {
        m_pSite->Release();
        m_pSite = NULL;
    }
    if (m_pInputObjectSite)
    {
        m_pInputObjectSite->Release();
        m_pInputObjectSite = NULL;
    }

    if (pUnkSite)
    {
        m_pSite = pUnkSite;
        m_pSite->AddRef();

        IOleWindow *pow;
        hr = pUnkSite->QueryInterface(IID_IOleWindow, reinterpret_cast<void **>(&pow));
        if (SUCCEEDED(hr))
        {
            hr = pow->GetWindow(&m_hwndParent);
            if (SUCCEEDED(hr))
            {
                WNDCLASSW wc = { 0 };
                wc.style         = CS_HREDRAW | CS_VREDRAW;
                wc.hCursor       = LoadCursor(NULL, IDC_ARROW);
                wc.hInstance     = g_hInst;
                wc.lpfnWndProc   = WndProc;
                wc.lpszClassName = g_szDeskBandClass;
                wc.hbrBackground = CreateSolidBrush(RGB(255, 255, 0));

                RegisterClassW(&wc);

                CreateWindowExW(0,
                                g_szDeskBandClass,
                                NULL,
                                WS_CHILD | WS_CLIPCHILDREN | WS_CLIPSIBLINGS,
                                0,
                                0,
                                0,
                                0,
                                m_hwndParent,
                                NULL,
                                g_hInst,
                                this);

                if (!m_hwnd)
                {
                    hr = E_FAIL;
                }
            }

            pow->Release();
        }

        if (SUCCEEDED(hr))
        {
            pUnkSite->QueryInterface(IID_PPV_ARGS(&m_pInputObjectSite));
        }
    }

    return hr;
}

STDMETHODIMP CDeskBand::GetSite(REFIID riid, void **ppv)
{
    HRESULT hr = E_FAIL;

    if (m_pSite)
    {
        hr =  m_pSite->QueryInterface(riid, ppv);
    }
    else
    {
        *ppv = NULL;
    }

    return hr;
}

//
// IInputObject
//
STDMETHODIMP CDeskBand::UIActivateIO(BOOL fActivate, MSG *)
{
    if (fActivate)
    {
        SetFocus(m_hwnd);
    }

    return S_OK;
}

STDMETHODIMP CDeskBand::HasFocusIO()
{
    return m_fHasFocus ? S_OK : S_FALSE;
}

STDMETHODIMP CDeskBand::TranslateAcceleratorIO(MSG *)
{
    return S_FALSE;
};

void CDeskBand::OnFocus(const BOOL fFocus)
{
    m_fHasFocus = fFocus;

    if (m_pInputObjectSite)
    {
        m_pInputObjectSite->OnFocusChangeIS(static_cast<IOleWindow*>(this), m_fHasFocus);
    }
}

void CDeskBand::OnPaint(const HDC hdcIn)
{
    HDC hdc = hdcIn;
    PAINTSTRUCT ps;
    static WCHAR szContent[] = L"D-speed: 140Kbps U-speed: 30Kbps";

    if (!hdc)
    {
        hdc = BeginPaint(m_hwnd, &ps);
    }

    if (hdc)
    {
        RECT rc;
        GetClientRect(m_hwnd, &rc);
        if (m_fCompositionEnabled)
        {
            HTHEME hTheme = OpenThemeData(NULL, L"TEXTSTYLE");
            if (hTheme)
            {
                HDC hdcPaint = NULL;
                HPAINTBUFFER hBufferedPaint = BeginBufferedPaint(hdc, &rc, BPBF_TOPDOWNDIB, NULL, &hdcPaint);

                HFONT hFont = ::CreateFontW(15, 0, 0, 0, FW_NORMAL, FALSE, FALSE, 0, ANSI_CHARSET, OUT_DEFAULT_PRECIS,
                    CLIP_DEFAULT_PRECIS, DEFAULT_QUALITY, DEFAULT_PITCH | FF_SWISS, L"@system");
                HFONT hOldFont = static_cast<HFONT>(::SelectObject(hdcPaint, hFont));
                GetTextColor(hdc);
                DrawThemeParentBackground(m_hwnd, hdcPaint, &rc);

                SIZE sRecv,sSend;
                GetTextExtentPointW(hdc, strRecv.c_str(), static_cast<int>(strRecv.length()), &sRecv);
                GetTextExtentPointW(hdc, strSend.c_str(), static_cast<int>(strSend.length()), &sSend);

                RECT rcRecv = { 0 };
                RECT rcSend = { 0 };
                rcSend.left = (RECTWIDTH(rc) - sSend.cx) / 2;
                rcSend.right = rcSend.left + sSend.cx;
                rcSend.top = RECTHEIGHT(rc) / 2 + (RECTHEIGHT(rc) / 2 - sSend.cy) / 2;
                rcSend.bottom = rcSend.top + sSend.cy;

                rcRecv.left = (RECTWIDTH(rc) - sRecv.cx) / 2;
                rcRecv.right = rcRecv.left + sRecv.cx;
                rcRecv.bottom = RECTHEIGHT(rc) / 2 - (RECTHEIGHT(rc) / 2 - sRecv.cy) / 2;
                rcRecv.top = rcRecv.bottom - sRecv.cy;

                DTTOPTS dttOptsRecv = { sizeof(dttOptsRecv) };
                dttOptsRecv.dwFlags = DTT_COMPOSITED | DTT_TEXTCOLOR | DTT_GLOWSIZE;
                dttOptsRecv.iGlowSize = 10;

                DTTOPTS dttOptsSend = dttOptsRecv;
                if (color == 1)
                {
                    dttOptsRecv.crText = RGB(1, 1, 1);
                    dttOptsSend.crText = RGB(1, 1, 1);
                }
                else if (color == 2)
                {
                    dttOptsRecv.crText = RGB(250, 250, 250);
                    dttOptsSend.crText = RGB(250, 250, 250);
                }
                else
                {
                    dttOptsRecv.crText = RGB(28, 200, 190);
                    dttOptsSend.crText = RGB(255, 160, 122);
                }

                DrawThemeTextEx(hTheme, hdcPaint, 0, 0, strRecv.c_str(), -1, 0, &rcRecv, &dttOptsRecv);
                DrawThemeTextEx(hTheme, hdcPaint, 0, 0, strSend.c_str(), -1, 0, &rcSend, &dttOptsSend);

                EndBufferedPaint(hBufferedPaint, TRUE);

                CloseThemeData(hTheme);
            }
        }
        else
        {
            SIZE sRecv, sSend;
            SetBkColor(hdc, RGB(255, 255, 0));
            GetTextExtentPointW(hdc, strRecv.c_str(), static_cast<int>(strRecv.length()), &sRecv);
            GetTextExtentPointW(hdc, strSend.c_str(), static_cast<int>(strRecv.length()), &sSend);
            TextOutW(hdc, (RECTWIDTH(rc) - sRecv.cx) / 2, (RECTHEIGHT(rc) - sRecv.cy) / 2, strRecv.c_str(), static_cast<int>(strRecv.length()));
            TextOutW(hdc, (RECTWIDTH(rc) - sSend.cx) / 2, ((RECTHEIGHT(rc) - sRecv.cy) / 2 ) + ((RECTHEIGHT(rc) - sSend.cy) / 2 ), strSend.c_str(), static_cast<int>(strSend.length()));
        }
    }

    if (!hdcIn)
    {
        EndPaint(m_hwnd, &ps);
    }
}

LRESULT CALLBACK CDeskBand::WndProc(HWND hwnd, UINT uMsg, WPARAM wParam, LPARAM lParam)
{
    LRESULT lResult = 0;

    CDeskBand *pDeskBand = reinterpret_cast<CDeskBand *>(GetWindowLongPtr(hwnd, GWLP_USERDATA));
    switch (uMsg)
    {
        case WM_CREATE:
            pDeskBand = reinterpret_cast<CDeskBand *>(reinterpret_cast<CREATESTRUCT *>(lParam)->lpCreateParams);
            pDeskBand->m_hwnd = hwnd;
            SetWindowLongPtr(hwnd, GWLP_USERDATA, reinterpret_cast<LONG_PTR>(pDeskBand));
            SetTimer(pDeskBand->m_hwnd,             // handle to main window 
                IDT_TIMER1,            // timer identifier 
                1000,                 // 1-second interval 
                (TIMERPROC)NULL);     // no timer callback 
            break;

        case WM_SETFOCUS:
            pDeskBand->OnFocus(TRUE);
            break;

        case WM_KILLFOCUS:
            pDeskBand->OnFocus(FALSE);
            break;

        case WM_PAINT:
            pDeskBand->OnPaint(NULL);
            break;

        case WM_PRINTCLIENT:
            pDeskBand->OnPaint(reinterpret_cast<HDC>(wParam));
            break;
    
        case WM_TIMER:
            switch (wParam)
            {
                case IDT_TIMER1:
                    pDeskBand->OnTimer();
            }
            break;

        case WM_ERASEBKGND:
            if (pDeskBand->m_fCompositionEnabled)
            {
                lResult = 1;
            }
            break;

        case WM_DESTROY:
            KillTimer(pDeskBand->m_hwnd, IDT_TIMER1);
            break;
    }

    if (uMsg != WM_ERASEBKGND)
    {
        lResult = DefWindowProc(hwnd, uMsg, wParam, lParam);
    }

    return lResult;
}

std::wstring Suffix(int suffix)
{
    return suffix == 4 ? L"Tbps" : suffix == 3 ? L"Gbps" : suffix == 2 ? L"Mbps" : suffix == 1 ? L"Kbps" : suffix == 0 ? L"bps" : L"Error";
}

void CDeskBand::OnTimer()
{
    std::stringstream stream;
    stream << std::fixed << std::setprecision(2) << down;
    std::string s = stream.str();

    strRecv.assign(L"D: ");
    strRecv.append(CA2W(s.c_str()));
    strRecv.append(Suffix(downSuffix));
    stream.str("");

    stream << std::fixed << std::setprecision(2) << up;
    s = stream.str();

    strSend.assign(L"U: ");
    strSend.append(CA2W(s.c_str()));
    strSend.append(Suffix(upSuffix));

    InvalidateRect(m_hwnd, NULL, FALSE);
    UpdateWindow(m_hwnd);
}