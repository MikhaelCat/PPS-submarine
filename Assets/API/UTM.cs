using System;

public static class UTM
{
    // Constants
    private const double UTM_PI = 3.14159265358979;
    private const double sm_a = 6378137.0;
    private const double sm_b = 6356752.314;
    private const double sm_EccSquared = 6.69437999013e-03;
    private const double UTMScaleFactor = 0.9996;

    // Convert degrees to radians
    private static double DegToRad(double deg)
    {
        return (deg / 180.0 * UTM_PI);
    }

    // Convert radians to degrees
    private static double RadToDeg(double rad)
    {
        return (rad / UTM_PI * 180.0);
    }

    // Computes the ellipsoidal distance from the equator to a point at a given latitude
    private static double ArcLengthOfMeridian(double phi)
    {
        double alpha, beta, gamma, delta, epsilon, n;
        double result;

        n = (sm_a - sm_b) / (sm_a + sm_b);
        alpha = ((sm_a + sm_b) / 2.0) * (1.0 + (Math.Pow(n, 2.0) / 4.0) + (Math.Pow(n, 4.0) / 64.0));
        beta = (-3.0 * n / 2.0) + (9.0 * Math.Pow(n, 3.0) / 16.0) + (-3.0 * Math.Pow(n, 5.0) / 32.0);
        gamma = (15.0 * Math.Pow(n, 2.0) / 16.0) + (-15.0 * Math.Pow(n, 4.0) / 32.0);
        delta = (-35.0 * Math.Pow(n, 3.0) / 48.0) + (105.0 * Math.Pow(n, 5.0) / 256.0);
        epsilon = (315.0 * Math.Pow(n, 4.0) / 512.0);

        result = alpha * (phi + (beta * Math.Sin(2.0 * phi))
            + (gamma * Math.Sin(4.0 * phi))
            + (delta * Math.Sin(6.0 * phi))
            + (epsilon * Math.Sin(8.0 * phi)));

        return result;
    }

    // Determines the central meridian for the given UTM zone
    private static double UTMCentralMeridian(int zone)
    {
        return DegToRad(-183.0 + ((double)zone * 6.0));
    }

    // Computes the footpoint latitude
    private static double FootpointLatitude(double y)
    {
        double y_, alpha_, beta_, gamma_, delta_, epsilon_, n;
        double result;

        n = (sm_a - sm_b) / (sm_a + sm_b);
        alpha_ = ((sm_a + sm_b) / 2.0) * (1 + (Math.Pow(n, 2.0) / 4) + (Math.Pow(n, 4.0) / 64));
        y_ = y / alpha_;
        beta_ = (3.0 * n / 2.0) + (-27.0 * Math.Pow(n, 3.0) / 32.0) + (269.0 * Math.Pow(n, 5.0) / 512.0);
        gamma_ = (21.0 * Math.Pow(n, 2.0) / 16.0) + (-55.0 * Math.Pow(n, 4.0) / 32.0);
        delta_ = (151.0 * Math.Pow(n, 3.0) / 96.0) + (-417.0 * Math.Pow(n, 5.0) / 128.0);
        epsilon_ = (1097.0 * Math.Pow(n, 4.0) / 512.0);

        result = y_ + (beta_ * Math.Sin(2.0 * y_))
            + (gamma_ * Math.Sin(4.0 * y_))
            + (delta_ * Math.Sin(6.0 * y_))
            + (epsilon_ * Math.Sin(8.0 * y_));

        return result;
    }

    // Converts lat/lon to UTM
    public static void LatLonToUTMXY(double lat, double lon, int zone, out double x, out double y)
    {
        if (zone < 1 || zone > 60)
            zone = (int)Math.Floor((lon + 180.0) / 6) + 1;

        MapLatLonToXY(DegToRad(lat), DegToRad(lon), UTMCentralMeridian(zone), out x, out y);

        x = x * UTMScaleFactor + 500000.0;
        y = y * UTMScaleFactor;
        if (y < 0.0)
            y = y + 10000000.0;
    }

    // Converts UTM to lat/lon
    public static void UTMXYToLatLon(double x, double y, int zone, bool southHemi, out double lat, out double lon)
    {
        double cmeridian;
        x -= 500000.0;
        x /= UTMScaleFactor;

        if (southHemi)
            y -= 10000000.0;

        y /= UTMScaleFactor;
        cmeridian = UTMCentralMeridian(zone);
        MapXYToLatLon(x, y, cmeridian, out lat, out lon);
    }

    // Converts lat/lon to x,y in Transverse Mercator projection
    private static void MapLatLonToXY(double phi, double lambda, double lambda0, out double x, out double y)
    {
        double N, nu2, ep2, t, t2, l;
        double l3coef, l4coef, l5coef, l6coef, l7coef, l8coef;

        ep2 = (Math.Pow(sm_a, 2.0) - Math.Pow(sm_b, 2.0)) / Math.Pow(sm_b, 2.0);
        nu2 = ep2 * Math.Pow(Math.Cos(phi), 2.0);
        N = Math.Pow(sm_a, 2.0) / (sm_b * Math.Sqrt(1 + nu2));
        t = Math.Tan(phi);
        t2 = t * t;
        l = lambda - lambda0;

        l3coef = 1.0 - t2 + nu2;
        l4coef = 5.0 - t2 + 9 * nu2 + 4.0 * (nu2 * nu2);
        l5coef = 5.0 - 18.0 * t2 + (t2 * t2) + 14.0 * nu2 - 58.0 * t2 * nu2;
        l6coef = 61.0 - 58.0 * t2 + (t2 * t2) + 270.0 * nu2 - 330.0 * t2 * nu2;
        l7coef = 61.0 - 479.0 * t2 + 179.0 * (t2 * t2) - (t2 * t2 * t2);
        l8coef = 1385.0 - 3111.0 * t2 + 543.0 * (t2 * t2) - (t2 * t2 * t2);

        x = N * Math.Cos(phi) * l
            + (N / 6.0 * Math.Pow(Math.Cos(phi), 3.0) * l3coef * Math.Pow(l, 3.0))
            + (N / 120.0 * Math.Pow(Math.Cos(phi), 5.0) * l5coef * Math.Pow(l, 5.0))
            + (N / 5040.0 * Math.Pow(Math.Cos(phi), 7.0) * l7coef * Math.Pow(l, 7.0));

        y = ArcLengthOfMeridian(phi)
            + (t / 2.0 * N * Math.Pow(Math.Cos(phi), 2.0) * Math.Pow(l, 2.0))
            + (t / 24.0 * N * Math.Pow(Math.Cos(phi), 4.0) * l4coef * Math.Pow(l, 4.0))
            + (t / 720.0 * N * Math.Pow(Math.Cos(phi), 6.0) * l6coef * Math.Pow(l, 6.0))
            + (t / 40320.0 * N * Math.Pow(Math.Cos(phi), 8.0) * l8coef * Math.Pow(l, 8.0));
    }

    // Converts x,y to lat/lon
    private static void MapXYToLatLon(double x, double y, double lambda0, out double phi, out double lambda)
    {
        double phif, Nf, Nfpow, nuf2, ep2, tf, tf2, tf4, cf;
        double x1frac, x2frac, x3frac, x4frac, x5frac, x6frac, x7frac, x8frac;
        double x2poly, x3poly, x4poly, x5poly, x6poly, x7poly, x8poly;

        phif = FootpointLatitude(y);
        ep2 = (Math.Pow(sm_a, 2.0) - Math.Pow(sm_b, 2.0)) / Math.Pow(sm_b, 2.0);
        cf = Math.Cos(phif);
        nuf2 = ep2 * Math.Pow(cf, 2.0);
        Nf = Math.Pow(sm_a, 2.0) / (sm_b * Math.Sqrt(1 + nuf2));
        Nfpow = Nf;
        tf = Math.Tan(phif);
        tf2 = tf * tf;
        tf4 = tf2 * tf2;

        x1frac = 1.0 / (Nfpow * cf);
        Nfpow = Nf * Nf;
        x2frac = tf / (2.0 * Nfpow);
        Nfpow = Nf * Nf * Nf;
        x3frac = 1.0 / (6.0 * Nfpow * cf);
        Nfpow = Nf * Nf * Nf * Nf;
        x4frac = tf / (24.0 * Nfpow);
        Nfpow = Nf * Nf * Nf * Nf * Nf;
        x5frac = 1.0 / (120.0 * Nfpow * cf);
        Nfpow = Nf * Nf * Nf * Nf * Nf * Nf;
        x6frac = tf / (720.0 * Nfpow);
        Nfpow = Nf * Nf * Nf * Nf * Nf * Nf * Nf;
        x7frac = 1.0 / (5040.0 * Nfpow * cf);
        Nfpow = Nf * Nf * Nf * Nf * Nf * Nf * Nf * Nf;
        x8frac = tf / (40320.0 * Nfpow);

        x2poly = -1.0 - nuf2;
        x3poly = -1.0 - 2 * tf2 - nuf2;
        x4poly = 5.0 + 3.0 * tf2 + 6.0 * nuf2 - 6.0 * tf2 * nuf2 - 3.0 * (nuf2 * nuf2) - 9.0 * tf2 * (nuf2 * nuf2);
        x5poly = 5.0 + 28.0 * tf2 + 24.0 * tf4 + 6.0 * nuf2 + 8.0 * tf2 * nuf2;
        x6poly = -61.0 - 90.0 * tf2 - 45.0 * tf4 - 107.0 * nuf2 + 162.0 * tf2 * nuf2;
        x7poly = -61.0 - 662.0 * tf2 - 1320.0 * tf4 - 720.0 * (tf4 * tf2);
        x8poly = 1385.0 + 3633.0 * tf2 + 4095.0 * tf4 + 1575 * (tf4 * tf2);

        phi = phif + x2frac * x2poly * (x * x)
            + x4frac * x4poly * Math.Pow(x, 4.0)
            + x6frac * x6poly * Math.Pow(x, 6.0)
            + x8frac * x8poly * Math.Pow(x, 8.0);

        lambda = lambda0 + x1frac * x
            + x3frac * x3poly * Math.Pow(x, 3.0)
            + x5frac * x5poly * Math.Pow(x, 5.0)
            + x7frac * x7poly * Math.Pow(x, 7.0);
    }

    // Helper: Convert Unity local coordinates to global lat/lon
    public static void LocalToGlobal(double localX, double localY, double localZ,
                                     double originLat, double originLon,
                                     out double latitude, out double longitude)
    {
        // Unity: X = East, Z = North, Y = Up (negative = depth)
        // Convert local meters to UTM offset
        LatLonToUTMXY(originLat, originLon, 0, out double originUTMX, out double originUTMY);

        double targetUTMX = originUTMX + localX;  // X = East
        double targetUTMY = originUTMY + localZ;  // Z = North

        // Determine zone and hemisphere
        int zone = (int)Math.Floor((originLon + 180.0) / 6) + 1;
        bool southHemi = originLat < 0;

        UTMXYToLatLon(targetUTMX, targetUTMY, zone, southHemi, out latitude, out longitude);
    }
}